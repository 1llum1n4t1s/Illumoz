using Google.Protobuf;
using Mozc.Renderer;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Renderer.Tests;

public class RendererCommandHandlerTests
{
    private static Size Measure(string s) => new(s.Length * 8, 16);
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    private static Pb.RendererCommand VisibleCommand()
    {
        var cw = new Pb.CandidateWindow();
        cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate
        {
            Index = 0, Value = "私",
            Annotation = new Pb.Annotation { Shortcut = "1", Description = "名詞" },
        });
        cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate { Index = 1, Value = "渡し" });
        return new Pb.RendererCommand
        {
            Visible = true,
            Output = new Pb.Output { CandidateWindow = cw },
            PreeditRectangle = new Pb.RendererCommand.Types.Rectangle
            {
                Left = 100, Top = 200, Right = 102, Bottom = 220,
            },
        };
    }

    [Fact]
    public void Handle_VisibleCommand_BuildsLayoutAndPosition()
    {
        RendererCommandHandler.Result r = RendererCommandHandler.Handle(VisibleCommand(), Measure, Screen);
        Assert.True(r.Visible);
        Assert.Equal(2, r.Rows.Count);
        Assert.Equal("私", r.Rows[0].Value);
        Assert.Equal("1", r.Rows[0].Shortcut);
        Assert.Equal("名詞", r.Rows[0].Description);
        Assert.NotNull(r.Layout);
        Assert.Equal(100, r.Position.X);   // キャレット左
        Assert.Equal(220, r.Position.Y);   // キャレット直下
    }

    [Fact]
    public void BuilderToHandler_RoundTrip()
    {
        var cw = new Pb.CandidateWindow();
        cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate { Index = 0, Value = "私" });
        var output = new Pb.Output { CandidateWindow = cw };
        var caret = new Rect(100, 200, 2, 20);

        Pb.RendererCommand cmd = RendererCommandBuilder.BuildUpdate(output, caret);
        Assert.True(cmd.Visible);
        Assert.Equal(Pb.RendererCommand.Types.CommandType.Update, cmd.Type);

        // protobuf を往復してから Handler に通す(ワイヤー経路の模擬)。
        Pb.RendererCommand parsed = Pb.RendererCommand.Parser.ParseFrom(cmd.ToByteArray());
        RendererCommandHandler.Result r = RendererCommandHandler.Handle(parsed, Measure, Screen);
        Assert.True(r.Visible);
        Assert.Equal("私", r.Rows[0].Value);
        Assert.Equal(220, r.Position.Y);
    }

    [Fact]
    public void BuildHide_NotVisible()
    {
        Pb.RendererCommand cmd = RendererCommandBuilder.BuildHide();
        Assert.False(cmd.Visible);
        RendererCommandHandler.Result r = RendererCommandHandler.Handle(cmd, Measure, Screen);
        Assert.False(r.Visible);
    }

    [Fact]
    public void Handle_NotVisible_ReturnsHidden()
    {
        var cmd = new Pb.RendererCommand { Visible = false };
        RendererCommandHandler.Result r = RendererCommandHandler.Handle(cmd, Measure, Screen);
        Assert.False(r.Visible);
        Assert.Empty(r.Rows);
        Assert.Null(r.Layout);
    }
}
