using Pb = Mozc.Commands;

namespace Mozc.Renderer;

// OS 統合層が renderer プロセスへ送る RendererCommand を組み立てる(C++ の
// renderer client 相当)。session の Output と現在のキャレット矩形から構築する。
public static class RendererCommandBuilder
{
    // output に候補窓があり候補が 1 件以上なら Visible=true の Update コマンドを返す。
    public static Pb.RendererCommand BuildUpdate(Pb.Output output, Rect caret)
    {
        bool visible = output.CandidateWindow != null && output.CandidateWindow.Candidate.Count > 0;
        var cmd = new Pb.RendererCommand
        {
            Type = Pb.RendererCommand.Types.CommandType.Update,
            Visible = visible,
            Output = output,
            PreeditRectangle = new Pb.RendererCommand.Types.Rectangle
            {
                Left = caret.Left,
                Top = caret.Top,
                Right = caret.Right,
                Bottom = caret.Bottom,
            },
        };
        return cmd;
    }

    // 候補窓を隠す Update コマンド(Visible=false)。
    public static Pb.RendererCommand BuildHide()
        => new() { Type = Pb.RendererCommand.Types.CommandType.Update, Visible = false };
}
