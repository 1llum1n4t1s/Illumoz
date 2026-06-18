using Mozc.Renderer;
using Xunit;

namespace Mozc.Renderer.Tests;

public class CandidateWindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Place_BelowCaret_WhenRoom()
    {
        var caret = new Rect(100, 200, 2, 20);
        Point p = CandidateWindowPlacement.Place(caret, new Size(150, 200), Screen);
        Assert.Equal(100, p.X);
        Assert.Equal(220, p.Y); // caret.Bottom
    }

    [Fact]
    public void Place_FlipsAbove_WhenNoRoomBelow()
    {
        var caret = new Rect(100, 1000, 2, 20); // 下に 60px しか無い
        Point p = CandidateWindowPlacement.Place(caret, new Size(150, 200), Screen);
        Assert.Equal(800, p.Y); // caret.Top(1000) - height(200)
    }

    [Fact]
    public void Place_ClampsRightEdge()
    {
        var caret = new Rect(1850, 200, 2, 20);
        Point p = CandidateWindowPlacement.Place(caret, new Size(150, 200), Screen);
        Assert.Equal(1770, p.X); // screen.Right(1920) - width(150)
    }

    [Fact]
    public void Place_ClampsLeftEdge()
    {
        var caret = new Rect(-50, 200, 2, 20);
        Point p = CandidateWindowPlacement.Place(caret, new Size(150, 200), Screen);
        Assert.Equal(0, p.X);
    }
}
