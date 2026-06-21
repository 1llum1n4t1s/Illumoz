using Mozc.Renderer;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Renderer.Tests;

public class PreeditViewBuilderTests
{
    private static Pb.Preedit.Types.Segment Seg(string value, Pb.Preedit.Types.Segment.Types.Annotation ann)
        => new()
        {
            Value = value,
            Annotation = ann,
            ValueLength = (uint)value.Length,
        };

    [Fact]
    public void Build_ConversionWithHighlight()
    {
        var p = new Pb.Preedit { Cursor = 2 };
        p.Segment.Add(Seg("今日", Pb.Preedit.Types.Segment.Types.Annotation.Highlight));
        p.Segment.Add(Seg("は", Pb.Preedit.Types.Segment.Types.Annotation.Underline));

        PreeditView v = PreeditViewBuilder.Build(p);
        Assert.Equal("今日は", v.Text);
        Assert.Equal(2, v.Segments.Count);
        Assert.Equal(PreeditStyle.Highlight, v.Segments[0].Style);
        Assert.Equal(PreeditStyle.Underline, v.Segments[1].Style);
        Assert.Equal(0, v.HighlightedSegmentIndex);
        Assert.Equal(2, v.Cursor);
    }

    [Fact]
    public void Build_NoHighlight_IndexMinusOne()
    {
        var p = new Pb.Preedit { Cursor = 0 };
        p.Segment.Add(Seg("あい", Pb.Preedit.Types.Segment.Types.Annotation.Underline));
        PreeditView v = PreeditViewBuilder.Build(p);
        Assert.Equal(-1, v.HighlightedSegmentIndex);
        Assert.Equal(PreeditStyle.Underline, v.Segments[0].Style);
    }

    [Fact]
    public void Build_NoneAnnotation_NormalStyle()
    {
        var p = new Pb.Preedit { Cursor = 1 };
        p.Segment.Add(Seg("a", Pb.Preedit.Types.Segment.Types.Annotation.None));
        PreeditView v = PreeditViewBuilder.Build(p);
        Assert.Equal(PreeditStyle.Normal, v.Segments[0].Style);
    }

    [Fact]
    public void Build_Empty_ReturnsEmpty()
    {
        PreeditView v = PreeditViewBuilder.Build(new Pb.Preedit { Cursor = 0 });
        Assert.Equal(string.Empty, v.Text);
        Assert.Empty(v.Segments);
        Assert.Equal(-1, v.HighlightedSegmentIndex);
    }

    [Fact]
    public void Build_Null_ReturnsEmpty()
    {
        PreeditView v = PreeditViewBuilder.Build(null);
        Assert.Empty(v.Segments);
    }
}
