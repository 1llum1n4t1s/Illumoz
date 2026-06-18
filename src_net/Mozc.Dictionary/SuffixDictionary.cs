using System.Collections.Generic;

namespace Mozc.Dictionary;

// C++ src/dictionary/suffix_dictionary.cc 相当。予測変換で文節末尾に付く
// 接尾語(「です」「ます」「さん」等)候補を供給する。key 昇順の配列に対し
// 接頭辞一致レンジを取り、SUFFIX_DICTIONARY トークンとして callback へ渡す。
public sealed class SuffixDictionary : DictionaryBase
{
    public readonly record struct Entry(string Key, string Value, ushort Lid, ushort Rid, int Cost);

    private readonly List<Entry> _entries; // Key 昇順(ordinal)。

    public SuffixDictionary(IEnumerable<Entry> entries)
    {
        _entries = new List<Entry>(entries);
        _entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
    }

    public override void LookupPredictive(string key, DictionaryCallback callback)
    {
        // key を接頭辞に持つエントリのレンジ [begin, end)。
        int begin = LowerBoundPrefix(key);
        for (int i = begin; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (!e.Key.StartsWith(key, global::System.StringComparison.Ordinal))
            {
                break; // 接頭辞を外れたら終了(昇順ゆえ以降も不一致)。
            }

            switch (callback.OnKey(e.Key))
            {
                case DictionaryCallback.ResultType.TraverseDone:
                    return;
                case DictionaryCallback.ResultType.TraverseNextKey:
                    continue;
                default:
                    break;
            }
            if (callback.OnActualKey(e.Key, e.Key, 0) == DictionaryCallback.ResultType.TraverseDone)
            {
                return;
            }

            var token = new Token
            {
                Key = e.Key,
                Value = string.IsNullOrEmpty(e.Value) ? e.Key : e.Value,
                Lid = e.Lid,
                Rid = e.Rid,
                Cost = e.Cost,
                Attributes = Token.Attribute.SuffixDictionary,
            };
            if (callback.OnToken(e.Key, e.Key, token) != DictionaryCallback.ResultType.TraverseContinue)
            {
                break;
            }
        }
    }

    // 接頭辞一致レンジの開始位置(key 以上の最初の要素)。
    private int LowerBoundPrefix(string key)
    {
        int lo = 0;
        int hi = _entries.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (string.CompareOrdinal(_entries[mid].Key, key) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }
}
