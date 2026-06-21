namespace Mozc.Renderer;

// カスケード(サブ)候補ウィンドウの配置。C++ src/renderer の cascading window 相当。
// 親候補ウィンドウの右側に並べ、右に収まらなければ左へ反転、上下は画面内クランプ。
public static class CascadingWindowPlacement
{
    // parent: 親ウィンドウの矩形。subSize: サブウィンドウの寸法。screen: 作業領域。
    public static Point Place(Rect parent, Size subSize, Rect screen)
    {
        // 既定は親の右肩(右隣・上端揃え)。
        int x = parent.Right;
        int y = parent.Top;

        // 右に収まらなければ親の左へ反転。
        if (x + subSize.Width > screen.Right)
        {
            x = parent.Left - subSize.Width;
        }
        // それでも左にはみ出すなら画面内へクランプ。
        if (x < screen.Left)
        {
            x = screen.Left;
        }

        // 下にはみ出すなら上へ詰める。
        if (y + subSize.Height > screen.Bottom)
        {
            y = screen.Bottom - subSize.Height;
        }
        if (y < screen.Top)
        {
            y = screen.Top;
        }

        return new Point(x, y);
    }
}
