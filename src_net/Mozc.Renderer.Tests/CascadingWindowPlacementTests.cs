using Mozc.Renderer;
using Xunit;

namespace Mozc.Renderer.Tests;

public class CascadingWindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Place_RightOfParent_WhenRoom()
    {
        var parent = new Rect(100, 200, 150, 180);
        Point p = CascadingWindowPlacement.Place(parent, new Size(120, 180), Screen);
        Assert.Equal(250, p.X); // parent.Right
        Assert.Equal(200, p.Y); // parent.Top
    }

    [Fact]
    public void Place_FlipsLeft_WhenNoRoomRight()
    {
        var parent = new Rect(1800, 200, 100, 180); // 右端付近、Right=1900
        Point p = CascadingWindowPlacement.Place(parent, new Size(120, 180), Screen);
        Assert.Equal(1680, p.X); // parent.Left(1800) - width(120)
    }

    [Fact]
    public void Place_ClampsLeft_WhenNoRoomEitherSide()
    {
        var parent = new Rect(50, 200, 1850, 180); // 画面幅一杯
        Point p = CascadingWindowPlacement.Place(parent, new Size(120, 180), Screen);
        Assert.Equal(0, p.X); // 左クランプ
    }

    [Fact]
    public void Place_ClampsBottom()
    {
        var parent = new Rect(100, 1000, 150, 60);
        Point p = CascadingWindowPlacement.Place(parent, new Size(120, 200), Screen);
        Assert.Equal(880, p.Y); // screen.Bottom(1080) - height(200)
    }
}
