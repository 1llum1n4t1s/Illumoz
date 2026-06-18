using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class SymbolRewriterTests
{
    private static Segments OneSegment(string key, string value)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        Candidate c = seg.AddCandidate();
        c.Key = key;
        c.Value = value;
        c.ContentKey = key;
        c.ContentValue = value;
        return segments;
    }

    [Fact]
    public void Rewrite_InsertsArrowSymbols()
    {
        var rewriter = new SymbolRewriter();
        Segments segments = OneSegment("やじるし", "矢印");

        Assert.True(rewriter.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("→", values);
        Assert.Contains("↑", values);
    }

    [Fact]
    public void Rewrite_CustomTable()
    {
        var table = new Dictionary<string, string[]> { ["てん"] = new[] { "・", "…" } };
        var rewriter = new SymbolRewriter(table);
        Segments segments = OneSegment("てん", "点");

        Assert.True(rewriter.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("・", values);
    }

    [Fact]
    public void Rewrite_UnknownKey_NoOp()
    {
        var rewriter = new SymbolRewriter();
        Segments segments = OneSegment("あいうえお", "愛");
        Assert.False(rewriter.Rewrite(segments));
    }
}
