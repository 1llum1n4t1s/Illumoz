using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/symbol_rewriter.cc の中核スライス。読み(やじるし/ほし/まる 等)に対し
// 対応する記号候補を挿入する。本家は symbol.tsv 由来の辞書を data から読むため、
// ここでは (読み -> 記号列) の辞書を注入可能にし、代表エントリの既定表も用意する。
public sealed class SymbolRewriter : IRewriter
{
    private readonly IReadOnlyDictionary<string, string[]> _table;

    public SymbolRewriter(IReadOnlyDictionary<string, string[]>? table = null)
        => _table = table ?? DefaultTable;

    // symbol.tsv の代表サブセット(DataGen で全量を構築して差し替える)。
    public static readonly IReadOnlyDictionary<string, string[]> DefaultTable =
        new Dictionary<string, string[]>
        {
            ["やじるし"] = new[] { "→", "←", "↑", "↓", "⇒", "⇔" },
            ["ほし"] = new[] { "★", "☆", "✦", "✧" },
            ["まる"] = new[] { "○", "◯", "●", "◎", "゜" },
            ["さんかく"] = new[] { "△", "▲", "▽", "▼" },
            ["しかく"] = new[] { "□", "■", "◇", "◆" },
            ["ばつ"] = new[] { "×", "✕", "✖" },
            ["おんぷ"] = new[] { "♪", "♫", "♬", "♩" },
            ["ゆうびん"] = new[] { "〒", "〶" },
            ["から"] = new[] { "〜", "～" },
            ["かっこ"] = new[] { "「」", "（）", "『』", "【】", "〈〉" },
        };

    // symbol.tsv(ヘッダ行 + 列1=記号, 列2=スペース区切り読み)を (読み->記号列) へ。
    // 読みトークンはひらがなのもののみ採用(記号自体や記号読みは除外)。出現順を保つ。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
    {
        var acc = new Dictionary<string, List<string>>();
        bool first = true;
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (first)
            {
                first = false; // ヘッダ行を飛ばす。
                continue;
            }
            if (row.Length == 0)
            {
                continue;
            }
            string[] cols = row.Split('\t');
            if (cols.Length < 3)
            {
                continue;
            }
            string symbol = cols[1];
            if (symbol.Length == 0)
            {
                continue;
            }
            foreach (string reading in cols[2].Split(' ', global::System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IsHiragana(reading))
                {
                    continue;
                }
                if (!acc.TryGetValue(reading, out List<string>? list))
                {
                    acc[reading] = list = new List<string>();
                }
                if (!list.Contains(symbol))
                {
                    list.Add(symbol);
                }
            }
        }
        var dict = new Dictionary<string, string[]>(acc.Count);
        foreach (KeyValuePair<string, List<string>> kv in acc)
        {
            dict[kv.Key] = kv.Value.ToArray();
        }
        return dict;
    }

    private static bool IsHiragana(string s)
    {
        foreach (char c in s)
        {
            if (c is < 'ぁ' or > 'ゖ' && c != 'ー' && c != '゛' && c != '゜')
            {
                return false;
            }
        }
        return s.Length > 0;
    }

    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int i = 0; i < segments.ConversionSegmentsSize; i++)
        {
            modified |= RewriteSegment(segments.ConversionSegment(i));
        }
        return modified;
    }

    private bool RewriteSegment(Segment segment)
    {
        if (segment.CandidatesSize == 0 || !_table.TryGetValue(segment.Key, out string[]? symbols))
        {
            return false;
        }

        var existing = new HashSet<string>();
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            existing.Add(segment.Get(i).Value);
        }

        Candidate baseCand = segment.Get(0);
        var newCands = new List<Candidate>();
        foreach (string sym in symbols)
        {
            if (existing.Add(sym))
            {
                newCands.Add(new Candidate
                {
                    Key = baseCand.Key,
                    Value = sym,
                    ContentKey = baseCand.ContentKey,
                    ContentValue = sym,
                    Description = "記号",
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        // C++ 同様、記号候補は少し下げた位置(先頭から数えて 3 番手付近)に挿入する。
        int insertIdx = global::System.Math.Min(3, segment.CandidatesSize);
        segment.InsertCandidates(insertIdx, newCands);
        return true;
    }
}
