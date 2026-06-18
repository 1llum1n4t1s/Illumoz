using Mozc.Base;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/transliteration_rewriter.cc の中核スライス。ひらがな読みから
// カタカナ・半角カタカナ・全角/半角ローマ字の T13n 候補を生成し挿入する。
public sealed class TransliterationRewriter : IRewriter
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
        string key = segment.Key;
        if (key.Length == 0 || segment.CandidatesSize == 0)
        {
            return false;
        }

        string katakana = JapaneseUtil.HiraganaToKatakana(key);
        var variants = new (string Value, string Desc)[]
        {
            (katakana, "カタカナ"),
            (JapaneseUtil.FullWidthKatakanaToHalfWidthKatakana(katakana), "半角カタカナ"),
            (JapaneseUtil.HiraganaToFullwidthRomanji(key), "全角ローマ字"),
            (JapaneseUtil.HiraganaToRomanji(key), "半角ローマ字"),
        };

        var existing = new HashSet<string>();
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            existing.Add(segment.Get(i).Value);
        }

        Candidate baseCand = segment.Get(0);
        var newCands = new List<Candidate>();
        foreach ((string value, string desc) in variants)
        {
            if (value.Length > 0 && existing.Add(value))
            {
                newCands.Add(new Candidate
                {
                    Key = key,
                    Value = value,
                    ContentKey = key,
                    ContentValue = value,
                    Description = desc,
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        // T13n は末尾付近に出す(変換候補を邪魔しない)。
        segment.InsertCandidates(segment.CandidatesSize, newCands);
        return true;
    }
}
