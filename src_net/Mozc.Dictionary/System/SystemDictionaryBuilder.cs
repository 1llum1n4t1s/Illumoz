using System.Text;
using Mozc.Base;
using Mozc.Storage.Louds;

namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/system_dictionary_builder.{h,cc} 相当。
// Token 列から system 辞書の 4 セクション画像(key/value/token/frequent pos)を生成する。
// SystemDictionary(reader)との往復検証に用いる。
public sealed class SystemDictionaryBuilder
{
    // 短いキーでは 1 バイトコスト符号化を使わない閾値(C++ flag 既定)。
    private const int MinKeyLengthToUseSmallCostEncoding = 6;

    public sealed class KeyInfo
    {
        public string Key = string.Empty;
        public int IdInKeyTrie = -1;
        public List<TokenInfo> Tokens = new();
    }

    private readonly SystemDictionaryCodec _codec = new();
    private readonly LoudsTrieBuilder _keyTrieBuilder = new();
    private readonly LoudsTrieBuilder _valueTrieBuilder = new();
    private readonly BitVectorBasedArrayBuilder _tokenArrayBuilder = new();
    // combined_pos -> frequent pos index。
    private readonly SortedDictionary<uint, int> _frequentPos = new();

    public byte[] ValueTrieImage { get; private set; } = global::System.Array.Empty<byte>();
    public byte[] KeyTrieImage { get; private set; } = global::System.Array.Empty<byte>();
    public byte[] TokenArrayImage { get; private set; } = global::System.Array.Empty<byte>();
    public byte[] FrequentPosImage { get; private set; } = global::System.Array.Empty<byte>();

    public void BuildFromTokens(IEnumerable<Token> tokens)
    {
        List<KeyInfo> keyInfoList = ReadTokens(tokens.ToList());

        BuildFrequentPos(keyInfoList);
        BuildValueTrie(keyInfoList);
        BuildKeyTrie(keyInfoList);

        SetIdForValue(keyInfoList);
        SetIdForKey(keyInfoList);
        SortTokenInfo(keyInfoList);
        SetCostType(keyInfoList);
        SetPosType(keyInfoList);
        SetValueType(keyInfoList);

        BuildTokenArray(keyInfoList);
        BuildSectionImages();
    }

    // SystemDictionary を直接構築して返す(往復検証の利便)。
    public SystemDictionary Build(IEnumerable<Token> tokens)
    {
        BuildFromTokens(tokens);
        return SystemDictionary.OpenFromSections(KeyTrieImage, ValueTrieImage, TokenArrayImage, FrequentPosImage);
    }

    private static uint CombinedPos(ushort lid, ushort rid) => ((uint)lid << 16) | rid;

    private static TokenInfo.ValueType GetValueType(Token token)
    {
        if (token.Value == token.Key)
        {
            return TokenInfo.ValueType.AsIsHiragana;
        }
        if (token.Value == JapaneseUtil.HiraganaToKatakana(token.Key))
        {
            return TokenInfo.ValueType.AsIsKatakana;
        }
        return TokenInfo.ValueType.DefaultValue;
    }

    // キーでグループ化(C++ は stable_sort 後に隣接グループ化。グループ間順序は
    // 最終マッピングに影響しないため、ここでは挿入順を保つグループ化で等価)。
    private List<KeyInfo> ReadTokens(List<Token> tokens)
    {
        foreach (Token t in tokens)
        {
            if (string.IsNullOrEmpty(t.Key) || string.IsNullOrEmpty(t.Value))
            {
                throw new InvalidOperationException("empty key/value in input");
            }
        }

        var order = new List<string>();
        var map = new Dictionary<string, KeyInfo>();
        foreach (Token token in tokens)
        {
            if (!map.TryGetValue(token.Key, out KeyInfo? info))
            {
                info = new KeyInfo { Key = token.Key };
                map[token.Key] = info;
                order.Add(token.Key);
            }
            info.Tokens.Add(new TokenInfo(token) { Value = GetValueType(token) });
        }
        return order.Select(k => map[k]).ToList();
    }

    private void BuildFrequentPos(List<KeyInfo> keyInfoList)
    {
        var posMap = new SortedDictionary<uint, int>();
        foreach (KeyInfo ki in keyInfoList)
        {
            foreach (TokenInfo ti in ki.Tokens)
            {
                uint pos = CombinedPos(ti.Token.Lid, ti.Token.Rid);
                posMap[pos] = posMap.GetValueOrDefault(pos) + 1;
            }
        }

        // 頻度ヒストグラム。
        var freqMap = new SortedDictionary<int, int>();
        foreach (int freq in posMap.Values)
        {
            freqMap[freq] = freqMap.GetValueOrDefault(freq) + 1;
        }

        // 頻度の高い側から最大 255 個に収まる閾値を求める。
        int numFreqPos = 0;
        int freqThreshold = int.MaxValue;
        foreach (KeyValuePair<int, int> kv in freqMap.Reverse())
        {
            if (numFreqPos + kv.Value > 255)
            {
                break;
            }
            freqThreshold = kv.Key;
            numFreqPos += kv.Value;
        }

        // 高頻度 pos を combined_pos 昇順で index 付け。
        int freqPosIdx = 0;
        foreach (KeyValuePair<uint, int> kv in posMap)
        {
            if (kv.Value >= freqThreshold)
            {
                _frequentPos[kv.Key] = freqPosIdx;
                freqPosIdx++;
            }
        }
    }

    private void BuildValueTrie(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            foreach (TokenInfo ti in ki.Tokens)
            {
                if (ti.Value is TokenInfo.ValueType.AsIsHiragana or TokenInfo.ValueType.AsIsKatakana)
                {
                    continue; // token array にフラグで入る。
                }
                _valueTrieBuilder.Add(_codec.EncodeValue(ti.Token.Value));
            }
        }
        ValueTrieImage = _valueTrieBuilder.Build();
    }

    private void SetIdForValue(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            foreach (TokenInfo ti in ki.Tokens)
            {
                ti.IdInValueTrie = _valueTrieBuilder.GetId(_codec.EncodeValue(ti.Token.Value));
            }
        }
    }

    private static void SortTokenInfo(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            // C++ コンパレータ: lid 降順, rid 降順, id_in_value_trie 昇順, attributes 昇順(安定)。
            ki.Tokens = ki.Tokens
                .Select((t, i) => (t, i))
                .OrderByDescending(x => x.t.Token.Lid)
                .ThenByDescending(x => x.t.Token.Rid)
                .ThenBy(x => x.t.IdInValueTrie)
                .ThenBy(x => (int)x.t.Token.Attributes)
                .ThenBy(x => x.i) // 安定化。
                .Select(x => x.t)
                .ToList();
        }
    }

    private void SetCostType(List<KeyInfo> keyInfoList)
    {
        // 複数の読みを持つ value(heterophone)。
        var heterophoneValues = new HashSet<string>();
        var seenReadingMap = new Dictionary<string, string>();
        foreach (KeyInfo ki in keyInfoList)
        {
            foreach (TokenInfo ti in ki.Tokens)
            {
                Token token = ti.Token;
                if (heterophoneValues.Contains(token.Value))
                {
                    continue;
                }
                if (!seenReadingMap.TryGetValue(token.Value, out string? reading))
                {
                    seenReadingMap[token.Value] = token.Key;
                    continue;
                }
                if (reading == token.Key)
                {
                    continue;
                }
                heterophoneValues.Add(token.Value);
            }
        }

        foreach (KeyInfo ki in keyInfoList)
        {
            if (CharsLen(ki.Key) < MinKeyLengthToUseSmallCostEncoding)
            {
                continue;
            }
            if (HasHomonymsInSamePos(ki))
            {
                continue;
            }
            if (ki.Tokens.Any(ti => heterophoneValues.Contains(ti.Token.Value)))
            {
                continue;
            }
            foreach (TokenInfo ti in ki.Tokens)
            {
                if (ti.Token.Cost < 0x100)
                {
                    continue;
                }
                ti.Cost = TokenInfo.CostType.CanUseSmallEncoding;
            }
        }
    }

    private static bool HasHomonymsInSamePos(KeyInfo keyInfo)
    {
        if (keyInfo.Tokens.Count == 1)
        {
            return false;
        }
        var seen = new HashSet<uint>();
        foreach (TokenInfo ti in keyInfo.Tokens)
        {
            if (!seen.Add(CombinedPos(ti.Token.Lid, ti.Token.Rid)))
            {
                return true;
            }
        }
        return false;
    }

    private void SetPosType(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            for (int i = 0; i < ki.Tokens.Count; i++)
            {
                TokenInfo ti = ki.Tokens[i];
                uint pos = CombinedPos(ti.Token.Lid, ti.Token.Rid);
                if (_frequentPos.TryGetValue(pos, out int idx))
                {
                    ti.Pos = TokenInfo.PosType.FrequentPos;
                    ti.IdInFrequentPosMap = idx;
                }
                if (i >= 1)
                {
                    uint prevPos = CombinedPos(ki.Tokens[i - 1].Token.Lid, ki.Tokens[i - 1].Token.Rid);
                    if (prevPos == pos)
                    {
                        ti.Pos = TokenInfo.PosType.SameAsPrevPos; // FREQUENT_POS を上書きしうる。
                    }
                }
            }
        }
    }

    private static void SetValueType(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            for (int i = 1; i < ki.Tokens.Count; i++)
            {
                TokenInfo prev = ki.Tokens[i - 1];
                TokenInfo cur = ki.Tokens[i];
                if (cur.Value != TokenInfo.ValueType.AsIsHiragana &&
                    cur.Value != TokenInfo.ValueType.AsIsKatakana &&
                    cur.Token.Value == prev.Token.Value)
                {
                    cur.Value = TokenInfo.ValueType.SameAsPrevValue;
                }
            }
        }
    }

    private void BuildKeyTrie(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            _keyTrieBuilder.Add(_codec.EncodeKey(ki.Key));
        }
        KeyTrieImage = _keyTrieBuilder.Build();
    }

    private void SetIdForKey(List<KeyInfo> keyInfoList)
    {
        foreach (KeyInfo ki in keyInfoList)
        {
            ki.IdInKeyTrie = _keyTrieBuilder.GetId(_codec.EncodeKey(ki.Key));
        }
    }

    private void BuildTokenArray(List<KeyInfo> keyInfoList)
    {
        // id_in_key_trie -> KeyInfo の逆引き表(id は一意かつ連続を仮定)。
        var idToKeyInfo = new KeyInfo[keyInfoList.Count];
        foreach (KeyInfo ki in keyInfoList)
        {
            idToKeyInfo[ki.IdInKeyTrie] = ki;
        }
        _tokenArrayBuilder.SetSize(4, 1); // C++ 既定(kMinTokenArrayBlobSize=4)。
        foreach (KeyInfo ki in idToKeyInfo)
        {
            _tokenArrayBuilder.Add(_codec.EncodeTokens(ki.Tokens));
        }
        _tokenArrayBuilder.Add(new[] { _codec.GetTokensTerminationFlag() });
        TokenArrayImage = _tokenArrayBuilder.Build();
    }

    private void BuildSectionImages()
    {
        // frequent pos: uint32[256](index 位置に combined_pos)。LE で直列化。
        var posArray = new byte[256 * 4];
        foreach (KeyValuePair<uint, int> kv in _frequentPos)
        {
            global::System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                posArray.AsSpan(kv.Value * 4), kv.Key);
        }
        FrequentPosImage = posArray;
    }

    // コードポイント数(C++ Util::CharsLen 相当)。
    private static int CharsLen(string s)
    {
        int count = 0;
        foreach (Rune _ in s.EnumerateRunes())
        {
            count++;
        }
        return count;
    }
}
