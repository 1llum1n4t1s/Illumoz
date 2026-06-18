using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class RemoveRedundantCandidateRewriterTests
{
    private static Segments Build(string key, params string[] values)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        foreach (string v in values)
        {
            Candidate c = seg.AddCandidate();
            c.Key = key;
            c.Value = v;
            c.ContentKey = key;
            c.ContentValue = v;
        }
        return segments;
    }

    [Fact]
    public void Rewrite_ClearsWhenSingleCandidateEqualsKey()
    {
        var segments = Build("あいう", "あいう");
        var rw = new RemoveRedundantCandidateRewriter();
        Assert.True(rw.Rewrite(segments));
        Assert.Equal(0, segments.ConversionSegment(0).CandidatesSize);
    }

    [Fact]
    public void Rewrite_KeepsWhenValueDiffers()
    {
        var segments = Build("あいう", "藍宇");
        var rw = new RemoveRedundantCandidateRewriter();
        Assert.False(rw.Rewrite(segments));
        Assert.Equal(1, segments.ConversionSegment(0).CandidatesSize);
    }

    [Fact]
    public void Rewrite_KeepsWhenMultipleCandidates()
    {
        var segments = Build("あいう", "あいう", "藍宇");
        var rw = new RemoveRedundantCandidateRewriter();
        Assert.False(rw.Rewrite(segments));
        Assert.Equal(2, segments.ConversionSegment(0).CandidatesSize);
    }
}
