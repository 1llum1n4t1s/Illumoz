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

    // symbol.tsv を (読み->記号列) へ(共通パーサに委譲)。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
        => Base.SymbolDataParser.ParseSymbol(tsv);

    // config.use_symbol_conversion=false で記号変換候補を出さない(C++ use_symbol_conversion() 相当)。
    public bool Enabled { get; set; } = true;

    public bool Rewrite(Segments segments)
    {
        if (!Enabled)
        {
            return false;
        }
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
