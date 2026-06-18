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
        if (baseIdx < 0)
        {
            return false;
        }

        Candidate baseCand = segment.Get(baseIdx);
        IReadOnlyList<NumberUtil.NumberString> variants =
            NumberUtil.ArabicToVariants(baseCand.Value);
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
}
