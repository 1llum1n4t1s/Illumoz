using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class SegmentsTests
{
    [Fact]
    public void Segment_KeyLen_CountsCodepoints()
    {
        var s = new Segment();
        s.SetKey("とうきょう");
        Assert.Equal(5, s.KeyLen); // 文字数(バイトではない)
        Assert.Equal("とうきょう", s.Key);
    }

    [Fact]
    public void Segment_CandidateManipulation()
    {
        var s = new Segment();
        s.AddCandidate().Value = "東京";
        s.AddCandidate().Value = "東協";
        Assert.Equal(2, s.CandidatesSize);
        Assert.Equal("東京", s.Get(0).Value);

        s.PushFrontCandidate().Value = "first";
        Assert.Equal("first", s.Get(0).Value);
        Assert.Equal(3, s.CandidatesSize);

        s.MoveCandidate(0, 2);
        Assert.Equal("first", s.Get(2).Value);

        s.EraseCandidate(2);
        Assert.Equal(2, s.CandidatesSize);
    }

    [Fact]
    public void Segment_MetaCandidates()
    {
        var s = new Segment();
        s.AddMetaCandidate().Value = "トウキョウ";
        Assert.Equal(1, s.MetaCandidatesSize);
        Assert.Equal("トウキョウ", s.MetaCandidate(0).Value);
        // メタ候補は負インデックスで有効。
        Assert.True(s.IsValidIndex(-1));
        Assert.False(s.IsValidIndex(-2));
    }

    [Fact]
    public void Candidate_FunctionalKeyValue()
    {
        var c = new Candidate { Key = "とうきょうと", ContentKey = "とうきょう", Value = "東京都", ContentValue = "東京" };
        Assert.Equal("と", c.FunctionalKey);
        Assert.Equal("都", c.FunctionalValue);
    }

    [Fact]
    public void Segments_HistoryConversionSplit()
    {
        var segs = new Segments();
        Segment h0 = segs.AddSegment();
        h0.Type = Segment.SegmentType.History;
        h0.SetKey("れきし");
        Segment h1 = segs.AddSegment();
        h1.Type = Segment.SegmentType.Submitted;
        h1.SetKey("かくてい");
        Segment c0 = segs.AddSegment();
        c0.SetKey("へんかん"); // Free

        Assert.Equal(3, segs.SegmentsSize);
        Assert.Equal(2, segs.HistorySegmentsSize);
        Assert.Equal(1, segs.ConversionSegmentsSize);
        Assert.Equal("へんかん", segs.ConversionSegment(0).Key);
        Assert.Equal("れきし", segs.HistorySegment(0).Key);
    }

    [Fact]
    public void Segments_ClearConversionKeepsHistory()
    {
        var segs = new Segments();
        segs.AddSegment().Type = Segment.SegmentType.History;
        segs.AddSegment(); // conversion
        segs.AddSegment(); // conversion

        segs.ClearConversionSegments();
        Assert.Equal(1, segs.SegmentsSize);
        Assert.Equal(1, segs.HistorySegmentsSize);
        Assert.Equal(0, segs.ConversionSegmentsSize);
    }

    [Fact]
    public void Segments_MaxHistoryClampedTo32()
    {
        var segs = new Segments();
        segs.SetMaxHistorySegmentsSize(100);
        Assert.Equal(32, segs.MaxHistorySegmentsSize);
        segs.SetMaxHistorySegmentsSize(4);
        Assert.Equal(4, segs.MaxHistorySegmentsSize);
    }
}
