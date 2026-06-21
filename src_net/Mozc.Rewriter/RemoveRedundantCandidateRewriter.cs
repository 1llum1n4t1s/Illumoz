using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/remove_redundant_candidate_rewriter.cc 相当(mixed_conversion 用)。
// 単一セグメント・単一候補で、その候補値が読み(key)と完全一致する冗長ケースは
// 候補を空にする。モバイルで preedit と同一だけの結果を返さないための処理。
public sealed class RemoveRedundantCandidateRewriter : IRewriter
{
    public bool Rewrite(Segments segments)
    {
        if (segments.ConversionSegmentsSize == 1)
        {
            Segment seg = segments.ConversionSegment(0);
            if (seg.CandidatesSize == 1 && seg.Get(0).Value == seg.Key)
            {
                seg.ClearCandidates();
                return true;
            }
        }
        return false;
    }
}
