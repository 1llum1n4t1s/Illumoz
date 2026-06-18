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

    // single_kanji.tsv(列0=読み, 列1=単漢字を連結した文字列)を読みテーブル化する。
    // 各文字(サロゲートペア対応)を 1 候補とする。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
    {
        var dict = new Dictionary<string, string[]>();
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (row.Length == 0 || row[0] == '#')
            {
                continue;
            }
            int tab = row.IndexOf('\t');
            if (tab <= 0 || tab == row.Length - 1)
            {
                continue;
            }
            string reading = row.Substring(0, tab);
            string chars = row.Substring(tab + 1);
            var list = new List<string>();
            var en = global::System.Globalization.StringInfo.GetTextElementEnumerator(chars);
            while (en.MoveNext())
            {
                list.Add((string)en.Current);
            }
            if (list.Count > 0)
            {
                dict[reading] = list.ToArray();
            }
        }
        return dict;
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
