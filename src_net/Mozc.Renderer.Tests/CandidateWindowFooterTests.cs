using Mozc.Renderer;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Renderer.Tests;

public class CandidateWindowFooterTests
{
    private static Pb.CandidateWindow WithFooter(uint focused, uint size, uint firstIndex, Pb.Footer footer)
    {
        var cw = new Pb.CandidateWindow { Size = size, FocusedIndex = focused, Footer = footer };
        cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate { Index = firstIndex, Value = "私" });
        cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate { Index = firstIndex + 1, Value = "渡し" });
        return cw;
    }

    [Fact]
    public void Build_IndexLabel_FirstPage()
    {
        var footer = new Pb.Footer { IndexVisible = true };
        FooterInfo f = CandidateWindowFooter.Build(WithFooter(focused: 2, size: 12, firstIndex: 0, footer));
        Assert.True(f.Visible);
        Assert.True(f.IndexVisible);
        Assert.Equal("3/12", f.IndexLabel); // 0+2+1 / 12
    }

    [Fact]
    public void Build_IndexLabel_PagedOffset()
    {
        var footer = new Pb.Footer { IndexVisible = true };
        // ページ先頭が 9 番目(index=9)、ページ内 focused=0 → 全体 10/120
        FooterInfo f = CandidateWindowFooter.Build(WithFooter(focused: 0, size: 120, firstIndex: 9, footer));
        Assert.Equal("10/120", f.IndexLabel);
    }

    [Fact]
    public void Build_NoIndex_WhenIndexInvisible()
    {
        var footer = new Pb.Footer { IndexVisible = false, Label = "状態", SubLabel = "v1", LogoVisible = true };
        FooterInfo f = CandidateWindowFooter.Build(WithFooter(focused: 0, size: 5, firstIndex: 0, footer));
        Assert.True(f.Visible);
        Assert.False(f.IndexVisible);
        Assert.Equal(string.Empty, f.IndexLabel);
        Assert.Equal("状態", f.Label);
        Assert.Equal("v1", f.SubLabel);
        Assert.True(f.LogoVisible);
    }

    [Fact]
    public void Build_NoFooter_NotVisible()
    {
        var cw = new Pb.CandidateWindow { Size = 0 };
        FooterInfo f = CandidateWindowFooter.Build(cw);
        Assert.False(f.Visible);
    }

    [Fact]
    public void Build_NullWindow_NotVisible()
    {
        FooterInfo f = CandidateWindowFooter.Build(null);
        Assert.False(f.Visible);
    }
}
