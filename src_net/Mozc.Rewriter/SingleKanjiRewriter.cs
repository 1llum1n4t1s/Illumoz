using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/single_kanji_rewriter.cc の中核スライス。読みに対応する単漢字を
// 候補末尾付近へ列挙する。本家は single_kanji 辞書(data)由来。ここでは
// (読み -> 単漢字列) 辞書を注入可能にする(DataGen で全量を構築して差し替える)。
public sealed class SingleKanjiRewriter : IRewriter
{
    private readonly IReadOnlyDictionary<string, string[]> _table;

    public SingleKanjiRewriter(IReadOnlyDictionary<string, string[]> table)
        => _table = table;

    // single_kanji.tsv を (読み->単漢字列) へ(共通パーサに委譲)。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
        => Base.SymbolDataParser.ParseSingleKanji(tsv);

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
        if (segment.CandidatesSize == 0 || !_table.TryGetValue(segment.Key, out string[]? kanji))
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
        foreach (string k in kanji)
        {
            if (existing.Add(k))
            {
                newCands.Add(new Candidate
                {
                    Key = baseCand.Key,
                    Value = k,
                    ContentKey = baseCand.ContentKey,
                    ContentValue = k,
                    Description = "単漢字",
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        // 単漢字は通常候補の後ろに出す。
        segment.InsertCandidates(segment.CandidatesSize, newCands);
        return true;
    }
}
