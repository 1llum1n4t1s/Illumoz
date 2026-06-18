using Mozc.Renderer;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Renderer.Tests;

public class UsageInfolistTests
{
    private static Pb.CandidateWindow Build(uint focused, params (int id, string value)[] cands)
    {
        var cw = new Pb.CandidateWindow { Size = (uint)cands.Length, FocusedIndex = focused };
        uint i = 0;
        foreach ((int id, string value) in cands)
        {
            cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate { Index = i++, Value = value, Id = id });
        }
        return cw;
    }

    private static Pb.InformationList Usages(params (int id, string title, string desc, int[] candIds)[] infos)
    {
        var list = new Pb.InformationList();
        foreach ((int id, string title, string desc, int[] candIds) in infos)
        {
            var info = new Pb.Information { Id = id, Title = title, Description = desc };
            info.CandidateId.AddRange(candIds);
            list.Information.Add(info);
        }
        return list;
    }

    [Fact]
    public void ForFocused_MatchesByCandidateId()
    {
        Pb.CandidateWindow cw = Build(focused: 1, (10, "私"), (20, "渡し"));
        cw.Usages = Usages(
            (1, "私", "わたし。一人称。", new[] { 10 }),
            (2, "渡し", "船などで渡すこと。", new[] { 20 }));

        UsageInfo? info = UsageInfolist.ForFocusedCandidate(cw);
        Assert.NotNull(info);
        Assert.Equal("渡し", info!.Value.Title);
        Assert.Equal("船などで渡すこと。", info.Value.Description);
    }

    [Fact]
    public void ForFocused_NoMatch_ReturnsNull()
    {
        Pb.CandidateWindow cw = Build(focused: 0, (10, "私"));
        cw.Usages = Usages((1, "他", "別語", new[] { 99 }));
        Assert.Null(UsageInfolist.ForFocusedCandidate(cw));
    }

    [Fact]
    public void ForFocused_NoUsages_ReturnsNull()
    {
        Pb.CandidateWindow cw = Build(focused: 0, (10, "私"));
        Assert.Null(UsageInfolist.ForFocusedCandidate(cw));
    }

    [Fact]
    public void ForFocused_OutOfRange_ReturnsNull()
    {
        Pb.CandidateWindow cw = Build(focused: 5, (10, "私"));
        cw.Usages = Usages((1, "私", "desc", new[] { 10 }));
        Assert.Null(UsageInfolist.ForFocusedCandidate(cw));
    }

    [Fact]
    public void ForFocused_NullWindow_ReturnsNull()
    {
        Assert.Null(UsageInfolist.ForFocusedCandidate(null));
    }
}
