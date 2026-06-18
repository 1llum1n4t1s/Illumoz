namespace Mozc.Renderer;

// C++ src/renderer の候補ウィンドウ配置計算相当。キャレット矩形・ウィンドウサイズ・
// 画面領域から候補窓の左上座標を決める。通常はキャレット直下、下に収まらなければ上へ反転、
// 左右は画面内へクランプする。プラットフォーム非依存(座標計算のみ)。
public static class CandidateWindowPlacement
{
    // caret: 変換対象のキャレット矩形(画面座標)。windowSize: 候補窓サイズ。
    // screen: モニタの作業領域。戻り値: 候補窓の左上座標。
    public static Point Place(Rect caret, Size windowSize, Rect screen)
    {
        // 縦: キャレット直下。下にはみ出すならキャレット上へ反転。
        int top = caret.Bottom;
        if (top + windowSize.Height > screen.Bottom)
        {
            int above = caret.Top - windowSize.Height;
            // 上にも収まらなければ、下端・上端のうち広い方へ寄せる(クランプ)。
            top = above >= screen.Top ? above
                : global::System.Math.Max(screen.Top, screen.Bottom - windowSize.Height);
        }

        // 横: キャレット左に揃え、右へはみ出すなら左へ寄せ、最後に左端でクランプ。
        int left = caret.Left;
        if (left + windowSize.Width > screen.Right)
        {
            left = screen.Right - windowSize.Width;
        }
        if (left < screen.Left)
        {
            left = screen.Left;
        }

        return new Point(left, top);
    }
}
