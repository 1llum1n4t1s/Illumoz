using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class DiceRewriterTests
{
    private static Segments Build(string key, int candidateCount = 1)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        for (int i = 0; i < candidateCount; i++)
        {
            Candidate c = seg.AddCandidate();
            c.Key = key;
            c.Value = key + i;
            c.ContentKey = key;
            c.ContentValue = key + i;
        }
        return segments;
    }

    [Fact]
    public void Rewrite_DiceKey_InsertsFaceCandidate()
    {
        var segments = Build("さいころ");
        var rw = new DiceRewriter(() => 4); // 出目固定
        Assert.True(rw.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        bool found = false;
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            if (seg.Get(i).Value == "4")
            {
                found = true;
                Assert.Equal("出た目の数", seg.Get(i).Description);
                Assert.True(seg.Get(i).Attributes.HasFlag(Candidate.Attribute.NoLearning));
            }
        }
        Assert.True(found);
    }

    [Fact]
    public void Rewrite_NonDiceKey_NoChange()
    {
        var segments = Build("にほん");
        var rw = new DiceRewriter(() => 3);
        Assert.False(rw.Rewrite(segments));
    }

    [Fact]
    public void Rewrite_InsertsAtPageEnd_WhenManyCandidates()
    {
        var segments = Build("さいころ", candidateCount: 12);
        var rw = new DiceRewriter(() => 6);
        Assert.True(rw.Rewrite(segments));
        // insert_pos = min(8, 12) = 8 → index 8 に出目候補。
        Assert.Equal("6", segments.ConversionSegment(0).Get(8).Value);
    }

    [Fact]
    public void DefaultRng_ProducesValidFace()
    {
        var segments = Build("さいころ");
        var rw = new DiceRewriter();
        Assert.True(rw.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        int face = int.Parse(seg.Get(seg.CandidatesSize - 1).Value);
        Assert.InRange(face, 1, 6);
    }
}
