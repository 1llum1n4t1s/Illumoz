using Mozc.Base;
using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class NumberRewriterTests
{
    [Theory]
    [InlineData("1234", "１２３４", "1,234", "千二百三十四")]
    [InlineData("10", "１０", "10", "十")]
    [InlineData("100", "１００", "100", "百")]
    [InlineData("20050", "２００５０", "20,050", "二万五十")]
    public void Variants_GeneratesExpected(string arabic, string wide, string sep, string kanji)
    {
        var vs = NumberUtil.ArabicToVariants(arabic);
        var values = new List<string>();
        foreach (NumberUtil.NumberString v in vs)
        {
            values.Add(v.Value);
        }
        Assert.Contains(wide, values);
        Assert.Contains(sep, values);
        Assert.Contains(kanji, values);
    }

    [Fact]
    public void Variants_OldKanji()
    {
        var vs = NumberUtil.ArabicToVariants("1234");
        var values = new List<string>();
        foreach (NumberUtil.NumberString v in vs)
        {
            values.Add(v.Value);
        }
        Assert.Contains("阡弐百参拾四", values);
    }

    [Fact]
    public void Variants_Zero()
    {
        var vs = NumberUtil.ArabicToVariants("0");
        var values = new List<string>();
        foreach (NumberUtil.NumberString v in vs)
        {
            values.Add(v.Value);
        }
        Assert.Contains("〇", values);
    }

    [Fact]
    public void Rewrite_InsertsNumberVariants()
    {
        var rewriter = new NumberRewriter();
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("1234");
        Candidate c = seg.AddCandidate();
        c.Key = "1234";
        c.Value = "1234";
        c.ContentKey = "1234";
        c.ContentValue = "1234";

        Assert.True(rewriter.Rewrite(segments));

        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("千二百三十四", values);
        Assert.Contains("1,234", values);
    }

    [Fact]
    public void Rewrite_KanjiNumberBase_AddsArabicAndVariants()
    {
        var rewriter = new NumberRewriter();
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("ひゃくにじゅう");
        // アラビア整数候補は無く、漢数字候補のみ。
        Candidate c = seg.AddCandidate();
        c.Key = "ひゃくにじゅう";
        c.Value = "百二十";
        c.ContentKey = "ひゃくにじゅう";
        c.ContentValue = "百二十";

        Assert.True(rewriter.Rewrite(segments));

        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("120", values);    // 桁解釈したアラビア数字
        Assert.Contains("１２０", values);  // 全角
    }

    [Fact]
    public void Rewrite_NonNumber_NoOp()
    {
        var rewriter = new NumberRewriter();
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("あいう");
        seg.AddCandidate().Value = "愛";
        Assert.False(rewriter.Rewrite(segments));
    }
}
