using System;
using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class FortuneRewriterTests
{
    private static Segments OneSegment(string key, params string[] values)
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

    [Theory]
    [InlineData(0, "大吉")]
    [InlineData(20, "大吉")]
    [InlineData(21, "吉")]
    [InlineData(40, "吉")]
    [InlineData(60, "中吉")]
    [InlineData(80, "小吉")]
    [InlineData(90, "末吉")]
    [InlineData(99, "凶")]
    public void Rewrite_NormalDay_MapsLevelToFortune(int level, string expected)
    {
        var segments = OneSegment("おみくじ", "御神籤");
        var rw = new FortuneRewriter(() => level, () => new DateTime(2026, 6, 19));
        Assert.True(rw.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        Candidate last = seg.Get(seg.CandidatesSize - 1);
        Assert.Equal(expected, last.Value);
        Assert.Equal("今日の運勢", last.Description);
        Assert.True((last.Attributes & Candidate.Attribute.NoLearning) != 0);
    }

    [Fact]
    public void Rewrite_NewYear_UsesNewYearLevels()
    {
        // 元日: level=30 は大吉(NewYearLevels[0]=30)。通常日なら吉。
        var segments = OneSegment("おみくじ", "御神籤");
        var rw = new FortuneRewriter(() => 30, () => new DateTime(2026, 1, 1));
        Assert.True(rw.Rewrite(segments));
        Assert.Equal("大吉", segments.ConversionSegment(0).Get(1).Value);
    }

    [Fact]
    public void Rewrite_NonOmikujiKey_NoChange()
    {
        var segments = OneSegment("さいころ", "賽子");
        var rw = new FortuneRewriter(() => 0, () => new DateTime(2026, 6, 19));
        Assert.False(rw.Rewrite(segments));
        Assert.Equal(1, segments.ConversionSegment(0).CandidatesSize);
    }
}
