using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class SingleKanjiRewriterTests
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
    public void Rewrite_AppendsSingleKanji()
    {
        var table = new Dictionary<string, string[]> { ["あ"] = new[] { "亜", "阿", "唖" } };
        var rewriter = new SingleKanjiRewriter(table);
        Segments segments = OneSegment("あ", "あ");

        Assert.True(rewriter.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("亜", values);
        Assert.Contains("阿", values);
    }

    [Fact]
    public void Rewrite_UnknownKey_NoOp()
    {
        var rewriter = new SingleKanjiRewriter(new Dictionary<string, string[]>());
        Assert.False(rewriter.Rewrite(OneSegment("あ", "あ")));
    }

    [Fact]
    public void Rewrite_Disabled_NoOp()
    {
        // config.use_single_kanji_conversion=false 相当: Enabled=false なら単漢字候補を出さない。
        var table = new Dictionary<string, string[]> { ["あ"] = new[] { "亜", "阿" } };
        var rewriter = new SingleKanjiRewriter(table) { Enabled = false };
        Segments segments = OneSegment("あ", "あ");
        Assert.False(rewriter.Rewrite(segments));
        Assert.Equal(1, segments.ConversionSegment(0).CandidatesSize);
    }
}
