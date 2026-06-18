using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class CalculatorRewriterTests
{
    private static Segments OneSegment(string key)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        Candidate c = seg.AddCandidate();
        c.Key = key;
        c.Value = key;
        c.ContentKey = key;
        c.ContentValue = key;
        return segments;
    }

    [Theory]
    [InlineData("1+2=", "3")]
    [InlineData("3*(4-1)", "9")]
    [InlineData("10/4", "2.5")]
    [InlineData("2*3+4", "10")]
    [InlineData("-5+8", "3")]
    [InlineData("１＋２＝", "3")] // 全角
    public void Calculate_ValidExpressions(string key, string expected)
    {
        Assert.True(CalculatorRewriter.TryCalculate(key, out string result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123")]      // 演算子なし
    [InlineData("あした")]    // 非数式
    [InlineData("1/0")]      // ゼロ除算
    [InlineData("1+")]       // 不完全
    [InlineData("(1+2")]     // 括弧不一致
    public void Calculate_InvalidExpressions(string key)
    {
        Assert.False(CalculatorRewriter.TryCalculate(key, out _));
    }

    [Fact]
    public void Rewrite_InsertsResultCandidates()
    {
        var rewriter = new CalculatorRewriter();
        Segments segments = OneSegment("1+2=");

        Assert.True(rewriter.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("3", values);
        Assert.Contains("1+2=3", values);
    }

    [Fact]
    public void Rewrite_MultipleSegments_NoOp()
    {
        var rewriter = new CalculatorRewriter();
        var segments = new Segments();
        foreach (string k in new[] { "1+2", "=3" })
        {
            Segment s = segments.AddSegment();
            s.SetKey(k);
            s.AddCandidate().Value = k;
        }
        Assert.False(rewriter.Rewrite(segments));
    }
}
