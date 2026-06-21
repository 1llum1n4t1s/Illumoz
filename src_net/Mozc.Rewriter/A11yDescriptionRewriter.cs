using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/a11y_description_rewriter.cc の rewriter 部。
// 各変換候補に読み上げ用説明(A11yDescription)を付与する。TalkBack 等の
// アクセシビリティ有効時のみ動かす想定だが、ここでは常時付与し OS 層で選択利用する。
public sealed class A11yDescriptionRewriter : IRewriter
{
    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int si = 0; si < segments.ConversionSegmentsSize; si++)
        {
            Segment seg = segments.ConversionSegment(si);
            for (int i = 0; i < seg.CandidatesSize; i++)
            {
                Candidate c = seg.Get(i);
                string desc = A11yDescription.Describe(c.Value);
                if (desc != c.A11yDescription)
                {
                    c.A11yDescription = desc;
                    modified = true;
                }
            }
        }
        return modified;
    }
}
