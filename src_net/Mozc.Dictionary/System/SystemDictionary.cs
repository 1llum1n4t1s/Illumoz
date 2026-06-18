using System.Buffers.Binary;
using System.Text;
using Mozc.Base;
using Mozc.Storage.Louds;

namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/system_dictionary.{h,cc} の前方探索相当。
// 4 セクション(key trie / value trie / token array / frequent pos)を束ね、
// LookupExact/Prefix/Predictive と HasKey/HasValue を提供する。
// reverse lookup(T13N/再変換用)は未移植(別途)。
public sealed class SystemDictionary : DictionaryBase
{
    private const int MaxDepth = LoudsTrie.MaxDepth;
    private const int LookupLimit = 64;

    private readonly LoudsTrie _keyTrie = new();
    private readonly LoudsTrie _valueTrie = new();
    private readonly BitVectorBasedArray _tokenArray = new();
    private uint[] _frequentPos = global::System.Array.Empty<uint>();
    private readonly SystemDictionaryCodec _codec = new();
    private readonly KeyExpansionTable _hiraganaExpansionTable = new();

    // C++ kHiraganaExpansionTable(かな修飾無視変換用)。
    private static readonly string[] HiraganaExpansionTable =
    {
        "ああぁ", "いいぃ", "ううぅゔ", "ええぇ", "おおぉ", "かかが",
        "ききぎ", "くくぐ", "けけげ", "ここご", "ささざ", "ししじ",
        "すすず", "せせぜ", "そそぞ", "たただ", "ちちぢ", "つつっづ",
        "っっづ", "ててで", "ととど", "ははばぱ", "ひひびぴ", "ふふぶぷ",
        "へへべぺ", "ほほぼぽ", "ややゃ", "ゆゆゅ", "よよょ", "わわゎ",
    };

    private SystemDictionary() { }

    // 4 セクションの生バイト列から構築(DictionaryFileCodec/DataSet からの読み出し後に渡す)。
    public static SystemDictionary OpenFromSections(
        byte[] keyImage, byte[] valueImage, byte[] tokenImage, byte[] posImage)
    {
        var dict = new SystemDictionary();
        if (!dict._keyTrie.Open(keyImage))
        {
            throw new InvalidDataException("cannot open key trie");
        }
        dict.BuildHiraganaExpansionTable();
        if (!dict._valueTrie.Open(valueImage))
        {
            throw new InvalidDataException("cannot open value trie");
        }
        dict._tokenArray.Open(tokenImage);
        dict._frequentPos = ReadUint32Array(posImage);
        return dict;
    }

    private static uint[] ReadUint32Array(byte[] image)
    {
        int n = image.Length / 4;
        var arr = new uint[n];
        for (int i = 0; i < n; i++)
        {
            arr[i] = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(i * 4));
        }
        return arr;
    }

    private void BuildHiraganaExpansionTable()
    {
        foreach (string entry in HiraganaExpansionTable)
        {
            byte[] encoded = Encoding.ASCII.GetBytes(_codec.EncodeKey(entry));
            if (encoded.Length <= 1)
            {
                continue;
            }
            _hiraganaExpansionTable.Add(encoded[0], encoded.AsSpan(1));
        }
    }

    public override bool HasKey(string key) => _keyTrie.HasKey(EncodeKeyBytes(key));

    public override bool HasValue(string value)
    {
        if (_valueTrie.HasKey(_codec.EncodeValue(value)))
        {
            return true;
        }
        // ひらがな/カタカナ/英字は value trie に入らず key trie にフラグで入る。
        string key = JapaneseUtil.KatakanaToHiragana(value);
        int keyId = _keyTrie.ExactSearch(EncodeKeyBytes(key));
        if (keyId == -1)
        {
            return false;
        }
        bool found = false;
        DecodeTokens(key, GetTokenBlob(keyId), token =>
        {
            if (value == token.Value)
            {
                found = true;
                return false; // stop
            }
            return true;
        });
        return found;
    }

    public override void LookupExact(string key, DictionaryCallback callback)
    {
        int keyId = _keyTrie.ExactSearch(EncodeKeyBytes(key));
        if (keyId == -1)
        {
            return;
        }
        if (callback.OnKey(key) != DictionaryCallback.ResultType.TraverseContinue)
        {
            return;
        }
        if (callback.OnActualKey(key, key, 0) != DictionaryCallback.ResultType.TraverseContinue)
        {
            return;
        }
        DecodeTokens(key, GetTokenBlob(keyId), token =>
            callback.OnToken(key, key, token) == DictionaryCallback.ResultType.TraverseContinue);
    }

    public override void LookupPrefix(string key, DictionaryCallback callback)
    {
        byte[] encodedKey = EncodeKeyBytes(key);
        if (!callback.IsKanaModifierInsensitiveConversion)
        {
            RunCallbackOnEachPrefix(key, encodedKey, callback);
            return;
        }
        var actualKeyBuffer = new byte[MaxDepth + 1];
        LookupPrefixWithKeyExpansionImpl(
            key, encodedKey, _hiraganaExpansionTable, callback,
            LoudsTrie.Root, 0, 0, actualKeyBuffer);
    }

    // キー展開なしの前方一致(C++ RunCallbackOnEachPrefix 相当)。
    private void RunCallbackOnEachPrefix(string key, byte[] encodedKey, DictionaryCallback callback)
    {
        var node = LoudsTrie.Root;
        for (int i = 0; i < encodedKey.Length;)
        {
            if (!_keyTrie.MoveToChildByLabel(encodedKey[i], ref node))
            {
                return;
            }
            i++;
            if (!_keyTrie.IsTerminalNode(node))
            {
                continue;
            }
            byte[] encodedPrefix = encodedKey.AsSpan(0, i).ToArray();
            string prefix = key.Substring(0, DecodedKeyCharLength(encodedPrefix));

            switch (callback.OnKey(prefix))
            {
                case DictionaryCallback.ResultType.TraverseDone:
                case DictionaryCallback.ResultType.TraverseCull:
                    return;
                case DictionaryCallback.ResultType.TraverseNextKey:
                    continue;
            }
            switch (callback.OnActualKey(prefix, prefix, 0))
            {
                case DictionaryCallback.ResultType.TraverseDone:
                case DictionaryCallback.ResultType.TraverseCull:
                    return;
                case DictionaryCallback.ResultType.TraverseNextKey:
                    continue;
            }

            int keyId = _keyTrie.GetKeyIdOfTerminalNode(node);
            bool nextKey = false;
            bool done = false;
            DecodeTokens(prefix, GetTokenBlob(keyId), token =>
            {
                DictionaryCallback.ResultType res = callback.OnToken(prefix, prefix, token);
                if (res is DictionaryCallback.ResultType.TraverseDone or DictionaryCallback.ResultType.TraverseCull)
                {
                    done = true;
                    return false;
                }
                if (res == DictionaryCallback.ResultType.TraverseNextKey)
                {
                    nextKey = true;
                    return false;
                }
                return true;
            });
            if (done)
            {
                return;
            }
            _ = nextKey; // NEXT_KEY は次プレフィックスへ進むだけ(ループ継続)。
        }
    }

    // キー展開ありの前方一致(C++ LookupPrefixWithKeyExpansionImpl 相当, DFS 再帰)。
    private DictionaryCallback.ResultType LookupPrefixWithKeyExpansionImpl(
        string key, byte[] encodedKey, KeyExpansionTable table, DictionaryCallback callback,
        LoudsNode node, int keyPos, int numExpanded, byte[] actualKeyBuffer)
    {
        // 終端ノードの処理(do-block を break で抜け走査フェーズへ)。
        for (bool once = true; once; once = false)
        {
            if (!_keyTrie.IsTerminalNode(node))
            {
                break;
            }
            byte[] encodedPrefix = encodedKey.AsSpan(0, keyPos).ToArray();
            string prefix = key.Substring(0, DecodedKeyCharLength(encodedPrefix));
            DictionaryCallback.ResultType result = callback.OnKey(prefix);
            if (result is DictionaryCallback.ResultType.TraverseDone or DictionaryCallback.ResultType.TraverseCull)
            {
                return result;
            }
            if (result == DictionaryCallback.ResultType.TraverseNextKey)
            {
                break;
            }

            string actualPrefix = _codec.DecodeKey(Encoding.UTF8.GetString(actualKeyBuffer, 0, keyPos));
            result = callback.OnActualKey(prefix, actualPrefix, numExpanded);
            if (result is DictionaryCallback.ResultType.TraverseDone or DictionaryCallback.ResultType.TraverseCull)
            {
                return result;
            }
            if (result == DictionaryCallback.ResultType.TraverseNextKey)
            {
                break;
            }

            int keyId = _keyTrie.GetKeyIdOfTerminalNode(node);
            DictionaryCallback.ResultType tokenResult = DictionaryCallback.ResultType.TraverseContinue;
            DecodeTokens(actualPrefix, GetTokenBlob(keyId), token =>
            {
                tokenResult = callback.OnToken(prefix, actualPrefix, token);
                if (tokenResult is DictionaryCallback.ResultType.TraverseDone
                    or DictionaryCallback.ResultType.TraverseCull
                    or DictionaryCallback.ResultType.TraverseNextKey)
                {
                    return false;
                }
                return true;
            });
            if (tokenResult is DictionaryCallback.ResultType.TraverseDone or DictionaryCallback.ResultType.TraverseCull)
            {
                return tokenResult;
            }
        }

        // 走査フェーズ。
        if (keyPos == encodedKey.Length)
        {
            return DictionaryCallback.ResultType.TraverseContinue;
        }
        byte currentChar = encodedKey[keyPos];
        ExpandedKey chars = table.ExpandKey(currentChar);
        var child = node;
        _keyTrie.MoveToFirstChild(ref child);
        while (_keyTrie.IsValidNode(child))
        {
            byte c = _keyTrie.GetEdgeLabelToParentNode(child);
            if (chars.IsHit(c))
            {
                actualKeyBuffer[keyPos] = c;
                DictionaryCallback.ResultType result = LookupPrefixWithKeyExpansionImpl(
                    key, encodedKey, table, callback, child, keyPos + 1,
                    numExpanded + (c != currentChar ? 1 : 0), actualKeyBuffer);
                if (result == DictionaryCallback.ResultType.TraverseDone)
                {
                    return DictionaryCallback.ResultType.TraverseDone;
                }
            }
            LoudsTrie.MoveToNextSibling(ref child);
        }
        return DictionaryCallback.ResultType.TraverseContinue;
    }

    public override void LookupPredictive(string key, DictionaryCallback callback)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        byte[] encodedKey = EncodeKeyBytes(key);
        if (encodedKey.Length > MaxDepth)
        {
            return;
        }
        KeyExpansionTable table = callback.IsKanaModifierInsensitiveConversion
            ? _hiraganaExpansionTable
            : KeyExpansionTable.Default;

        var states = CollectPredictiveNodesInBfsOrder(encodedKey, table, LookupLimit);

        foreach (PredictiveState state in states)
        {
            int keyId = _keyTrie.GetKeyIdOfTerminalNode(state.Node);
            byte[] encodedActual = _keyTrie.RestoreKeyBytes(keyId);
            string encodedActualSuffix =
                Encoding.UTF8.GetString(encodedActual, encodedKey.Length, encodedActual.Length - encodedKey.Length);
            string decodedKey = key + _codec.DecodeKey(encodedActualSuffix);

            switch (callback.OnKey(decodedKey))
            {
                case DictionaryCallback.ResultType.TraverseDone:
                    return;
                case DictionaryCallback.ResultType.TraverseNextKey:
                    continue;
            }

            string actualKey = state.NumExpanded > 0
                ? _codec.DecodeKey(Encoding.UTF8.GetString(encodedActual))
                : decodedKey;

            switch (callback.OnActualKey(decodedKey, actualKey, state.NumExpanded))
            {
                case DictionaryCallback.ResultType.TraverseDone:
                    return;
                case DictionaryCallback.ResultType.TraverseNextKey:
                    continue;
            }

            bool returnAll = false;
            DecodeTokens(actualKey, GetTokenBlob(keyId), token =>
            {
                DictionaryCallback.ResultType res = callback.OnToken(decodedKey, actualKey, token);
                if (res == DictionaryCallback.ResultType.TraverseDone)
                {
                    returnAll = true;
                    return false;
                }
                if (res == DictionaryCallback.ResultType.TraverseNextKey)
                {
                    return false;
                }
                return true;
            });
            if (returnAll)
            {
                return;
            }
        }
    }

    private readonly struct PredictiveState
    {
        public readonly LoudsNode Node;
        public readonly int KeyPos;
        public readonly int NumExpanded;
        public PredictiveState(LoudsNode node, int keyPos, int numExpanded)
        {
            Node = node;
            KeyPos = keyPos;
            NumExpanded = numExpanded;
        }
    }

    private List<PredictiveState> CollectPredictiveNodesInBfsOrder(
        byte[] encodedKey, KeyExpansionTable table, int limit)
    {
        var result = new List<PredictiveState>();
        var queue = new Queue<PredictiveState>();
        queue.Enqueue(new PredictiveState(LoudsTrie.Root, 0, 0));
        do
        {
            PredictiveState state = queue.Dequeue();

            if (state.KeyPos < encodedKey.Length)
            {
                byte targetChar = encodedKey[state.KeyPos];
                ExpandedKey chars = table.ExpandKey(targetChar);
                var node = state.Node;
                _keyTrie.MoveToFirstChild(ref node);
                while (_keyTrie.IsValidNode(node))
                {
                    byte c = _keyTrie.GetEdgeLabelToParentNode(node);
                    if (chars.IsHit(c))
                    {
                        int numExpanded = state.NumExpanded + (c != targetChar ? 1 : 0);
                        queue.Enqueue(new PredictiveState(node, state.KeyPos + 1, numExpanded));
                    }
                    LoudsTrie.MoveToNextSibling(ref node);
                }
                continue;
            }

            if (_keyTrie.IsTerminalNode(state.Node))
            {
                result.Add(state);
            }

            if (result.Count > limit)
            {
                int maxKeyLen = state.KeyPos;
                while (queue.Count > 0)
                {
                    state = queue.Dequeue();
                    if (state.KeyPos > maxKeyLen)
                    {
                        break;
                    }
                    if (_keyTrie.IsTerminalNode(state.Node))
                    {
                        result.Add(state);
                    }
                }
                break;
            }

            var child = state.Node;
            _keyTrie.MoveToFirstChild(ref child);
            while (_keyTrie.IsValidNode(child))
            {
                queue.Enqueue(new PredictiveState(child, state.KeyPos + 1, state.NumExpanded));
                LoudsTrie.MoveToNextSibling(ref child);
            }
        } while (queue.Count > 0);
        return result;
    }

    // encoded key は変換後文字列の UTF-8 バイト列(かな域は 1 バイト、他は多バイト)。
    private byte[] EncodeKeyBytes(string key) => Encoding.UTF8.GetBytes(_codec.EncodeKey(key));

    private int DecodedKeyCharLength(byte[] encodedPrefix)
    {
        // encoded プレフィックスをデコードした「文字数」(key.Substring 用)。
        string decoded = _codec.DecodeKey(Encoding.UTF8.GetString(encodedPrefix));
        return decoded.Length;
    }

    private ReadOnlyMemory<byte> GetTokenBlob(int keyId) => _tokenArray.Get(keyId);

    // C++ TokenDecodeIterator 相当。token ブロブを復号し、各 Token に handler を適用。
    // handler が false を返すと打ち切り。SAME_AS_PREV 系の状態は引き継ぐ。
    private void DecodeTokens(string key, ReadOnlyMemory<byte> blob, Func<Token, bool> handler)
    {
        ReadOnlySpan<byte> ptr = blob.Span;
        int offset = 0;
        string katakanaKey = string.Empty;

        // 再利用される作業用 Token(SAME_AS_PREV で前トークンの値を保持)。
        var work = new Token { Key = key };
        int prevIdInValueTrie = -1;

        while (true)
        {
            var info = new TokenInfo(work);
            work.Attributes = Token.Attribute.None;

            bool hasNext = _codec.DecodeToken(ptr.Slice(offset), info, out int read);
            offset += read;

            switch (info.Value)
            {
                case TokenInfo.ValueType.DefaultValue:
                    work.Value = _codec.DecodeValue(_valueTrie.RestoreKeyBytes(info.IdInValueTrie));
                    prevIdInValueTrie = info.IdInValueTrie;
                    break;
                case TokenInfo.ValueType.SameAsPrevValue:
                    info.IdInValueTrie = prevIdInValueTrie;
                    // work.Value は据え置き(前トークンの値)。
                    break;
                case TokenInfo.ValueType.AsIsHiragana:
                    work.Value = key;
                    break;
                case TokenInfo.ValueType.AsIsKatakana:
                    if (key.Length > 0 && katakanaKey.Length == 0)
                    {
                        katakanaKey = JapaneseUtil.HiraganaToKatakana(key);
                    }
                    work.Value = katakanaKey;
                    break;
            }

            if (info.Pos == TokenInfo.PosType.FrequentPos)
            {
                uint pos = _frequentPos[info.IdInFrequentPosMap];
                work.Lid = (ushort)(pos >> 16);
                work.Rid = (ushort)(pos & 0xffff);
            }

            // handler には現在の Token のスナップショットを渡す。
            var emitted = new Token(work.Key, work.Value, work.Cost, work.Lid, work.Rid, work.Attributes);
            bool cont = handler(emitted);
            if (!cont || !hasNext)
            {
                return;
            }
        }
    }
}
