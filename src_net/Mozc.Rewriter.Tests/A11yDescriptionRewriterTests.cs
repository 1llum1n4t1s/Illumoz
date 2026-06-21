using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class A11yDescriptionRewriterTests
{
    private static Segments OneCandidate(string value)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(value);
        Candidate c = seg.AddCandidate();
        c.Key = value;
        c.Value = value;
        c.ContentKey = value;
        c.ContentValue = value;
        return segments;
    }

    [Fact]
    public void Rewrite_SetsA11yDescription()
    {
        var segments = OneCandidate("あい");
        var rw = new A11yDescriptionRewriter();
        Assert.True(rw.Rewrite(segments));
        Assert.Equal("あい。ヒラガナ あい", segments.ConversionSegment(0).Get(0).A11yDescription);
    }

    [Fact]
    public void Rewrite_KanjiOnly_DescriptionIsValueOnly()
    {
        var segments = OneCandidate("漢");
        var rw = new A11yDescriptionRewriter();
        // 漢字のみは説明なし(本体のみ) → A11yDescription は値と同じ。既定が空なので変更あり。
        Assert.True(rw.Rewrite(segments));
        Assert.Equal("漢", segments.ConversionSegment(0).Get(0).A11yDescription);
    }
}
