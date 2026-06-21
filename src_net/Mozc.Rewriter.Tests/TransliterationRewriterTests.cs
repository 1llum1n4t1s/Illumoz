using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class TransliterationRewriterTests
{
    [Fact]
    public void Rewrite_AddsKatakanaAndRomaji()
    {
        var rewriter = new TransliterationRewriter();
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("あいう");
        Candidate c = seg.AddCandidate();
        c.Key = "あいう";
        c.Value = "愛烏";
        c.ContentKey = "あいう";
        c.ContentValue = "愛烏";

        Assert.True(rewriter.Rewrite(segments));

        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("アイウ", values);   // 全角カタカナ
        Assert.Contains("ｱｲｳ", values);     // 半角カタカナ
        Assert.Contains("aiu", values);     // 半角ローマ字
    }

    [Fact]
    public void Rewrite_EmptyKey_NoOp()
    {
        var rewriter = new TransliterationRewriter();
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("");
        seg.AddCandidate().Value = "x";
        Assert.False(rewriter.Rewrite(segments));
    }

    [Fact]
    public void Rewrite_Disabled_NoOp()
    {
        // config.use_t13n_conversion=false 相当: Enabled=false なら T13n 候補を一切付与しない。
        var rewriter = new TransliterationRewriter { Enabled = false };
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey("あいう");
        Candidate c = seg.AddCandidate();
        c.Key = "あいう";
        c.Value = "愛烏";
        c.ContentKey = "あいう";
        c.ContentValue = "愛烏";
        Assert.False(rewriter.Rewrite(segments));
        Assert.Equal(1, seg.CandidatesSize);
    }
}
