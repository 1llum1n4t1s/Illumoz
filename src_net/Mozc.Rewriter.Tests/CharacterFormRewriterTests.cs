using Mozc.Base;
using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class CharacterFormRewriterTests
{
    private static Segments OneCandidate(string value)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(value);
        Candidate c = seg.AddCandidate();
        c.Key = value;
        c.Value = value;
        c.ContentKey = value;
        c.ContentValue = value;
        return segments;
    }

    [Fact]
    public void Rewrite_AppliesHalfWidthToNumbers()
    {
        var m = CharacterFormManager.FromRules(new[] { ("0", CharacterForm.HalfWidth) });
        var rw = new CharacterFormRewriter(m);
        var segments = OneCandidate("１２３");
        Assert.True(rw.Rewrite(segments));
        Assert.Equal("123", segments.ConversionSegment(0).Get(0).Value);
    }

    [Fact]
    public void Rewrite_NoRuleNoChange()
    {
        var m = new CharacterFormManager(); // ルールなし
        var rw = new CharacterFormRewriter(m);
        var segments = OneCandidate("１２３");
        Assert.False(rw.Rewrite(segments));
        Assert.Equal("１２３", segments.ConversionSegment(0).Get(0).Value);
    }

    [Fact]
    public void Rewrite_KanjiUnchanged()
    {
        var rw = new CharacterFormRewriter(CharacterFormManager.CreatePreeditDefault());
        var segments = OneCandidate("漢字");
        Assert.False(rw.Rewrite(segments));
    }

    [Fact]
    public void SetManager_SwitchesBehavior()
    {
        var rw = new CharacterFormRewriter(new CharacterFormManager());
        var segments = OneCandidate("123");
        Assert.False(rw.Rewrite(segments)); // 初期はルールなし

        rw.SetManager(CharacterFormManager.FromRules(new[] { ("0", CharacterForm.FullWidth) }));
        var segments2 = OneCandidate("123");
        Assert.True(rw.Rewrite(segments2));
        Assert.Equal("１２３", segments2.ConversionSegment(0).Get(0).Value);
    }
}
