using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class DateRewriterTests
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
    public void RewriteToday_InsertsDateFormats()
    {
        var clock = new FixedClock(new global::System.DateTime(2026, 6, 19, 0, 0, 0)); // 金曜
        var rewriter = new DateRewriter(clock);
        Segments segments = OneSegment("きょう", "今日");

        Assert.True(rewriter.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("2026/06/19", values);
        Assert.Contains("2026-06-19", values);
        Assert.Contains("2026年6月19日", values);
        Assert.Contains("金曜日", values);
        // 元候補 今日 は保持。
        Assert.Contains("今日", values);
    }

    [Fact]
    public void RewriteNow_InsertsTimeFormats()
    {
        var clock = new FixedClock(new global::System.DateTime(2026, 6, 19, 14, 30, 0));
        var rewriter = new DateRewriter(clock);
        Segments segments = OneSegment("いま", "今");

        Assert.True(rewriter.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("14:30", values);
        Assert.Contains("14時30分", values);
        Assert.Contains("14時半", values);
        Assert.Contains("午後2時30分", values);
        Assert.Contains("午後2時半", values);
    }

    [Fact]
    public void NonDateKey_NoChange()
    {
        var rewriter = new DateRewriter(new FixedClock(new global::System.DateTime(2026, 6, 19)));
        Segments segments = OneSegment("わたし", "私");
        Assert.False(rewriter.Rewrite(segments));
        Assert.Equal(1, segments.ConversionSegment(0).CandidatesSize);
    }

    [Fact]
    public void Merger_RunsRewriters()
    {
        var merger = new RewriterMerger();
        merger.AddRewriter(new DateRewriter(new FixedClock(new global::System.DateTime(2026, 6, 19))));
        Segments segments = OneSegment("ことし", "今年");
        Assert.True(merger.Rewrite(segments));
        var values = new List<string>();
        Segment seg = segments.ConversionSegment(0);
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("2026", values);
        Assert.Contains("2026年", values);
    }
}
