using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

public class HistoryReconstructorTests
{
    // Number/UniqueNoun/GeneralNoun に判別可能な ID を入れた PosMatcher。
    private const ushort NumberId = 100;
    private const ushort UniqueNounId = 200;
    private const ushort GeneralNounId = 300;

    private static PosMatcher MakePosMatcher()
    {
        int n = PosMatcher.RuleCount;
        var data = new ushort[n + n + 1];
        int sentinel = n + n;
        data[sentinel] = 0xFFFF;
        for (int i = 0; i < n; i++)
        {
            data[n + i] = (ushort)sentinel;
        }
        data[(int)PosMatcher.Rule.Number] = NumberId;
        data[(int)PosMatcher.Rule.UniqueNoun] = UniqueNounId;
        data[(int)PosMatcher.Rule.GeneralNoun] = GeneralNounId;
        return new PosMatcher(data, n);
    }

    [Fact]
    public void Reconstruct_NumberTail_CreatesHistorySegment()
    {
        var r = new HistoryReconstructor(MakePosMatcher());
        var segments = new Segments();
        Assert.True(r.ReconstructHistory("買い物は１２３", segments));

        Segment seg = segments.GetSegment(0);
        Assert.Equal(Segment.SegmentType.History, seg.Type);
        Candidate c = seg.Get(0);
        Assert.Equal("123", c.Key);        // 半角化された読み
        Assert.Equal("１２３", c.Value);    // 表記は元の全角
        Assert.Equal(NumberId, c.Lid);
        Assert.True(c.Attributes.HasFlag(Candidate.Attribute.NoLearning));
    }

    [Fact]
    public void Reconstruct_AlphabetTail_UsesUniqueNounId()
    {
        var r = new HistoryReconstructor(MakePosMatcher());
        var segments = new Segments();
        Assert.True(r.ReconstructHistory("これはABC", segments));
        Candidate c = segments.GetSegment(0).Get(0);
        Assert.Equal("ABC", c.Value);
        Assert.Equal(UniqueNounId, c.Lid);
    }

    [Fact]
    public void Reconstruct_AllowsSingleTrailingSpace()
    {
        var r = new HistoryReconstructor(MakePosMatcher());
        var segments = new Segments();
        Assert.True(r.ReconstructHistory("abc ", segments));
        Assert.Equal("abc", segments.GetSegment(0).Get(0).Value);
    }

    [Theory]
    [InlineData("こんにちは")]   // 末尾がひらがな
    [InlineData("漢字")]         // 末尾が漢字
    [InlineData("")]             // 空
    [InlineData("ab  ")]         // 末尾空白2つ
    public void Reconstruct_NonConnective_ReturnsFalse(string text)
    {
        var r = new HistoryReconstructor(MakePosMatcher());
        var segments = new Segments();
        Assert.False(r.ReconstructHistory(text, segments));
        Assert.Equal(0, segments.SegmentsSize);
    }
}
