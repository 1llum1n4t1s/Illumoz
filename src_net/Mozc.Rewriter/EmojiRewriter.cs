using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/emoji_rewriter.cc の中核スライス。読みに対応する絵文字を候補へ挿入する。
// 本家は emoji_data(data)由来。ここでは (読み->絵文字列) 辞書を注入可能にし、
// emoji_data.tsv をパースするローダを用意する。
public sealed class EmojiRewriter : IRewriter
{
    private readonly IReadOnlyDictionary<string, string[]> _table;

    public EmojiRewriter(IReadOnlyDictionary<string, string[]> table) => _table = table;

    // emoji_data.tsv(# はコメント、列1=絵文字グリフ, 列2=スペース区切り読み)を
    // (読み->絵文字列) へ。読みはひらがなトークンのみ採用、出現順保持。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
    {
        var acc = new Dictionary<string, List<string>>();
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (row.Length == 0 || row[0] == '#')
            {
                continue;
            }
            string[] cols = row.Split('\t');
            if (cols.Length < 3)
            {
                continue;
            }
            string glyph = cols[1];
            if (glyph.Length == 0)
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
                if (!list.Contains(glyph))
                {
                    list.Add(glyph);
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
        if (segment.CandidatesSize == 0 || !_table.TryGetValue(segment.Key, out string[]? emojis))
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
        foreach (string e in emojis)
        {
            if (existing.Add(e))
            {
                newCands.Add(new Candidate
                {
                    Key = baseCand.Key,
                    Value = e,
                    ContentKey = baseCand.ContentKey,
                    ContentValue = e,
                    Description = "絵文字",
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        // 絵文字は候補末尾に出す。
        segment.InsertCandidates(segment.CandidatesSize, newCands);
        return true;
    }
}
