using Pb = Mozc.Commands;

namespace Mozc.Renderer;

// RendererCommand(protobuf)を解釈し、候補ウィンドウの表示状態・レイアウト・配置を算出する。
// C++ src/renderer の Renderer::ExecCommand 相当の中核(描画は GUI 層)。
public static class RendererCommandHandler
{
    public readonly record struct Result(
        bool Visible,
        IReadOnlyList<CandidateRow> Rows,
        TableLayout? Layout,
        Point Position,
        FooterInfo Footer);

    // command: RendererCommand。measure: 文字列→寸法。screen: モニタ作業領域。
    public static Result Handle(
        Pb.RendererCommand command,
        Func<string, Size> measure,
        Rect screen)
    {
        if (!command.Visible || command.Output?.CandidateWindow == null
            || command.Output.CandidateWindow.Candidate.Count == 0)
        {
            return new Result(false, global::System.Array.Empty<CandidateRow>(), null, default, default);
        }

        Pb.CandidateWindow cw = command.Output.CandidateWindow;
        var rows = new List<CandidateRow>(cw.Candidate.Count);
        foreach (Pb.CandidateWindow.Types.Candidate c in cw.Candidate)
        {
            rows.Add(new CandidateRow(
                c.Annotation?.Shortcut ?? string.Empty,
                c.Value,
                c.Annotation?.Description ?? string.Empty));
        }

        TableLayout layout = CandidateWindowLayouter.Build(rows, measure);
        Rect caret = ToCaret(command.PreeditRectangle);
        Point pos = CandidateWindowPlacement.PlaceFromLayout(layout, caret, screen);
        FooterInfo footer = CandidateWindowFooter.Build(cw);
        return new Result(true, rows, layout, pos, footer);
    }

    private static Rect ToCaret(Pb.RendererCommand.Types.Rectangle? r)
    {
        if (r == null)
        {
            return new Rect(0, 0, 0, 0);
        }
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
