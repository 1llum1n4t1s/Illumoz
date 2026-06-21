using System.Collections.Generic;
using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class EnglishVariantsRewriterTests
{
    [Fact]
    public void Expand_LowercaseWord_AddsCapitalizedAndUpper()
    {
        Assert.True(EnglishVariantsRewriter.ExpandEnglishVariants("google", out List<string> v));
        Assert.Equal(new[] { "Google", "GOOGLE" }, v);
    }

    [Fact]
    public void Expand_Capitalized_AddsLowerAndUpper()
    {
        Assert.True(EnglishVariantsRewriter.ExpandEnglishVariants("Google", out List<string> v));
        Assert.Equal(new[] { "google", "GOOGLE" }, v);
    }

    [Fact]
    public void Expand_NonStandard_OnlyLower()
    {
        Assert.True(EnglishVariantsRewriter.ExpandEnglishVariants("iMac", out List<string> v));
        Assert.Equal(new[] { "imac" }, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")] // 空白あり
    [InlineData("あ")]          // 非ASCII(lower==upper)
    public void Expand_Rejected(string input)
    {
        Assert.False(EnglishVariantsRewriter.ExpandEnglishVariants(input, out _));
    }

    [Fact]
    public void Rewrite_InsertsVariantsForT13NCandidate()
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("ぐーぐる");
        Candidate c = seg.AddCandidate();
        c.Key = "ぐーぐる";
        c.Value = "google";
        c.ContentKey = "ぐーぐる";
        c.ContentValue = "google";

        var rw = new EnglishVariantsRewriter();
        Assert.True(rw.Rewrite(segments));

        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("google", values);
        Assert.Contains("Google", values);
        Assert.Contains("GOOGLE", values);
    }

    [Fact]
    public void Rewrite_NonEnglishCandidate_NoChange()
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("にほん");
        Candidate c = seg.AddCandidate();
        c.Key = "にほん";
        c.Value = "日本";
        c.ContentKey = "にほん";
        c.ContentValue = "日本";

        var rw = new EnglishVariantsRewriter();
        Assert.False(rw.Rewrite(segments));
    }
}
