using Mozc.Base;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/number_rewriter.cc の中核スライス。値がアラビア整数の候補に対し
// 全角/区切り/漢数字/大字の各表記を候補として挿入する。
public sealed class NumberRewriter : IRewriter
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
        // 値がアラビア整数の最初の候補を基準にする。
        int baseIdx = -1;
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            if (NumberUtil.IsDecimalInteger(segment.Get(i).Value))
            {
                baseIdx = i;
                break;
            }
        }

        // アラビア整数候補が無ければ、漢数字候補(百二十 等)を桁解釈してアラビア化し基準にする。
        string baseArabic;
        if (baseIdx >= 0)
        {
            baseArabic = segment.Get(baseIdx).Value;
        }
        else
        {
            baseIdx = FindKanjiNumberBase(segment, out baseArabic);
            if (baseIdx < 0)
            {
                return false;
            }
        }

        Candidate baseCand = segment.Get(baseIdx);
        IReadOnlyList<NumberUtil.NumberString> variants =
            NumberUtil.ArabicToVariants(baseArabic);
        if (variants.Count == 0)
        {
            return false;
        }

        var existing = new HashSet<string>();
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            existing.Add(segment.Get(i).Value);
        }

        var newCands = new List<Candidate>();
        // 漢数字基準のときは素のアラビア数字(120)も候補に加える。
        if (existing.Add(baseArabic))
        {
            newCands.Add(new Candidate
            {
                Key = baseCand.Key,
                Value = baseArabic,
                ContentKey = baseCand.ContentKey,
                ContentValue = baseArabic,
                Description = "数字",
                Cost = baseCand.Cost,
            });
        }
        foreach (NumberUtil.NumberString v in variants)
        {
            if (existing.Add(v.Value))
            {
                newCands.Add(new Candidate
                {
                    Key = baseCand.Key,
                    Value = v.Value,
                    ContentKey = baseCand.ContentKey,
                    ContentValue = v.Value,
                    Description = v.Description,
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        segment.InsertCandidates(baseIdx + 1, newCands);
        return true;
    }

    // 値が漢数字(百二十 等)で桁解釈可能な最初の候補を探し、そのアラビア表記を返す。
    // 既にアラビア化された値(content_value と一致)は対象外。
    private static int FindKanjiNumberBase(Segment segment, out string arabic)
    {
        arabic = string.Empty;
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            string value = segment.Get(i).Value;
            if (NumberUtil.TryNormalizeNumber(value, trimLeadingZeros: true, out string a)
                && a != value)
            {
                arabic = a;
                return i;
            }
        }
        return -1;
    }
}
