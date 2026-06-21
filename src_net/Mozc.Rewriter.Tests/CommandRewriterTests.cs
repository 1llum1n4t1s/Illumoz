using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class CommandRewriterTests
{
    private static Segments Build(string key, params string[] values)
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

    private static Candidate? FindByCommand(Segment seg, Candidate.CommandType cmd)
    {
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            if (seg.Get(i).Command == cmd)
            {
                return seg.Get(i);
            }
        }
        return null;
    }

    [Fact]
    public void Rewrite_IncognitoTrigger_InsertsEnableCommand()
    {
        var segments = Build("しーくれっと", "シークレット");
        var rw = new CommandRewriter();
        Assert.True(rw.Rewrite(segments));
        Candidate? c = FindByCommand(segments.ConversionSegment(0), Candidate.CommandType.EnableIncognitoMode);
        Assert.NotNull(c);
        Assert.Equal("シークレットモードをオン", c!.Value);
        Assert.Equal("【", c.Prefix);
        Assert.Equal("】", c.Suffix);
        Assert.True(c.Attributes.HasFlag(Candidate.Attribute.CommandCandidate));
    }

    [Fact]
    public void Rewrite_IncognitoOn_InsertsDisableCommand()
    {
        var segments = Build("ひみつ", "秘密");
        var rw = new CommandRewriter { IncognitoMode = true };
        Assert.True(rw.Rewrite(segments));
        Candidate? c = FindByCommand(segments.ConversionSegment(0), Candidate.CommandType.DisableIncognitoMode);
        Assert.NotNull(c);
        Assert.Equal("シークレットモードをオフ", c!.Value);
    }

    [Fact]
    public void Rewrite_SuggestTrigger_InsertsToggle()
    {
        var segments = Build("さじぇすと", "サジェスト");
        var rw = new CommandRewriter();
        Assert.True(rw.Rewrite(segments));
        Candidate? c = FindByCommand(segments.ConversionSegment(0), Candidate.CommandType.EnablePresentationMode);
        Assert.NotNull(c);
        Assert.Equal("サジェスト機能の一時停止", c!.Value);
    }

    [Fact]
    public void Rewrite_SuggestDisabled_NoCommandInserted()
    {
        var segments = Build("さじぇすと", "サジェスト");
        var rw = new CommandRewriter { SuggestionEnabled = false };
        // C++ も RewriteSegment は true を返すが、サジェスト無効時はコマンド候補を挿入しない。
        Assert.True(rw.Rewrite(segments));
        Assert.Null(FindByCommand(segments.ConversionSegment(0), Candidate.CommandType.EnablePresentationMode));
    }

    [Fact]
    public void Rewrite_NonTriggerKey_NoChange()
    {
        var segments = Build("にほん", "日本");
        var rw = new CommandRewriter();
        Assert.False(rw.Rewrite(segments));
    }

    [Fact]
    public void Rewrite_CommandValue_InsertsBothToggles()
    {
        var segments = Build("こまんど", "コマンド");
        var rw = new CommandRewriter();
        Assert.True(rw.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        Assert.NotNull(FindByCommand(seg, Candidate.CommandType.EnableIncognitoMode));
        Assert.NotNull(FindByCommand(seg, Candidate.CommandType.EnablePresentationMode));
    }
}
