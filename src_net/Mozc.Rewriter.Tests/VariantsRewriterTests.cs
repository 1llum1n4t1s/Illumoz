using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class VariantsRewriterTests
{
    private static Segments OneSegment(string key, string value)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        Candidate c = seg.AddCandidate();
        c.Key = key; c.Value = value; c.ContentKey = key; c.ContentValue = value;
        return segments;
    }

    private static List<string> Values(Segment seg)
    {
        var v = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++) { v.Add(seg.Get(i).Value); }
        return v;
    }

    [Fact]
    public void HalfAscii_AddsFullWidth()
    {
        var rewriter = new VariantsRewriter();
        Segments segments = OneSegment("abc", "abc");
        Assert.True(rewriter.Rewrite(segments));
        Assert.Contains("ａｂｃ", Values(segments.ConversionSegment(0)));
    }

    [Fact]
    public void FullKatakana_AddsHalfWidth()
    {
        var rewriter = new VariantsRewriter();
        Segments segments = OneSegment("あいう", "アイウ");
        Assert.True(rewriter.Rewrite(segments));
        Assert.Contains("ｱｲｳ", Values(segments.ConversionSegment(0)));
    }

    [Fact]
    public void Kanji_NoOp()
    {
        var rewriter = new VariantsRewriter();
        Assert.False(rewriter.Rewrite(OneSegment("やま", "山")));
    }
}
