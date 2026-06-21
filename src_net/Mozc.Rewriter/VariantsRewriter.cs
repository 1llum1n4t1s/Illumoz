using Mozc.Base;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/variants_rewriter.cc の中核スライス。候補値に半角/全角の別形が
// あれば対応する form を別候補として補う(ASCII↔全角英数, 全角↔半角カタカナ)。
public sealed class VariantsRewriter : IRewriter
{
    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int i = 0; i < segments.ConversionSegmentsSize; i++)
        {
            modified |= RewriteSegment(segments.ConversionSegment(i));
        }
        return modified;
    }

    private static bool RewriteSegment(Segment segment)
    {
        var existing = new HashSet<string>();
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            existing.Add(segment.Get(i).Value);
        }

        // スナップショット(列挙中の挿入を避ける)。
        int baseCount = segment.CandidatesSize;
        var additions = new List<(int Index, Candidate Cand)>();
        for (int i = 0; i < baseCount; i++)
        {
            Candidate c = segment.Get(i);
            string? alt = AlternateForm(c.Value);
            if (alt != null && existing.Add(alt))
            {
                additions.Add((i, new Candidate
                {
                    Key = c.Key,
                    Value = alt,
                    ContentKey = c.ContentKey,
                    ContentValue = alt,
                    Description = DescribeForm(alt),
                    Cost = c.Cost,
                }));
            }
        }
        if (additions.Count == 0)
        {
            return false;
        }
        // 後ろの index から挿入してずれを防ぐ。各別形は元候補の直後へ。
        for (int k = additions.Count - 1; k >= 0; k--)
        {
            segment.InsertCandidate(additions[k].Index + 1, additions[k].Cand);
        }
        return true;
    }

    // 値の主たる字種に応じて 1 つの別形を返す(無ければ null)。
    private static string? AlternateForm(string value)
    {
        if (value.Length == 0)
        {
            return null;
        }
        if (ContainsHalfAscii(value))
        {
            string alt = JapaneseUtil.HalfWidthAsciiToFullWidthAscii(value);
            return alt != value ? alt : null;
        }
        if (ContainsFullAscii(value))
        {
            string alt = JapaneseUtil.FullWidthAsciiToHalfWidthAscii(value);
            return alt != value ? alt : null;
        }
        if (ContainsFullKatakana(value))
        {
            string alt = JapaneseUtil.FullWidthKatakanaToHalfWidthKatakana(value);
            return alt != value ? alt : null;
        }
        if (ContainsHalfKatakana(value))
        {
            string alt = JapaneseUtil.HalfWidthKatakanaToFullWidthKatakana(value);
            return alt != value ? alt : null;
        }
        return null;
    }

    private static string DescribeForm(string value) =>
        ContainsHalfAscii(value) || ContainsHalfKatakana(value) ? "[半]" : "[全]";

    private static bool ContainsHalfAscii(string s)
    {
        foreach (char c in s) { if (c is >= '!' and <= '~') { return true; } }
        return false;
    }

    private static bool ContainsFullAscii(string s)
    {
        foreach (char c in s) { if (c is >= '！' and <= '～') { return true; } }
        return false;
    }

    private static bool ContainsFullKatakana(string s)
    {
        foreach (char c in s) { if (c is >= 'ァ' and <= 'ヶ' || c == 'ー') { return true; } }
        return false;
    }

    private static bool ContainsHalfKatakana(string s)
    {
        foreach (char c in s) { if (c is >= '｡' and <= 'ﾟ') { return true; } }
        return false;
    }
}
