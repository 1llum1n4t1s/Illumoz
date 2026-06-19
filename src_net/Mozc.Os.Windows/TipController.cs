using System.Linq;
using Google.Protobuf;
using Pb = Mozc.Commands;

namespace Mozc.Os.Windows;

// TSF の各 sink(ITfKeyEventSink 等)が呼ぶ TIP 本体ロジック(C#)。
// protobuf(commands.proto)で IPC 越しに mozc_server と会話する。
// transport は Func<byte[],byte[]>(proto Input → proto Output)。実体は名前付きパイプ client、
// テストでは in-proc EngineServer.HandleProtoRequest を注入する。COM 境界は別(ComExports)。
public sealed class TipController
{
    private readonly Func<byte[], byte[]> _transport;
    private ulong _sessionId;
    private bool _hasSession;

    public TipController(Func<byte[], byte[]> transport) => _transport = transport;

    public string Preedit { get; private set; } = string.Empty;
    public string LastCommit { get; private set; } = string.Empty;
    public IReadOnlyList<string> Candidates { get; private set; } = global::System.Array.Empty<string>();
    // 候補のショートカット文字(SelectionShortcut)。
    public IReadOnlyList<string> Shortcuts { get; private set; } = global::System.Array.Empty<string>();
    // 候補の説明(記号/全角 等)。
    public IReadOnlyList<string> Descriptions { get; private set; } = global::System.Array.Empty<string>();
    // 候補窓が入力中サジェスト(C++ category=SUGGESTION)か。
    public bool IsSuggestion { get; private set; }

    private Pb.Output Send(Pb.Input input) => Pb.Output.Parser.ParseFrom(_transport(input.ToByteArray()));

    public void EnsureSession()
    {
        if (_hasSession)
        {
            return;
        }
        Pb.Output o = Send(new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession });
        // id=0(MaxSessions 到達等で失敗)は確立扱いにしない。確立扱いすると以降ずっと
        // session id 0 でキーを送り続け、COM 再生成まで再試行しなくなるため(ImeClient と同じガード)。
        if (o.Id == 0)
        {
            _hasSession = false;
            return;
        }
        _sessionId = o.Id;
        _hasSession = true;
    }

    // 文字キー(printable)。
    public bool OnCharacter(char c)
        => OnKey(new Pb.KeyEvent { KeyCode = c });

    // 特殊キー(Space/Enter/Backspace 等)。
    public bool OnSpecialKey(Pb.KeyEvent.Types.SpecialKey special)
        => OnKey(new Pb.KeyEvent { SpecialKey = special });

    private bool OnKey(Pb.KeyEvent key)
    {
        EnsureSession();
        Pb.Output o = Send(new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = _sessionId,
            Key = key,
        });
        Update(o);
        return o.Consumed;
    }

    private void Update(Pb.Output o)
    {
        LastCommit = o.Result != null ? o.Result.Value : string.Empty;
        Preedit = o.Preedit != null && o.Preedit.Segment.Count > 0 ? o.Preedit.Segment[0].Value : string.Empty;
        Candidates = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Value).ToList()
            : global::System.Array.Empty<string>();
        Shortcuts = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Annotation?.Shortcut ?? string.Empty).ToList()
            : global::System.Array.Empty<string>();
        Descriptions = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Annotation?.Description ?? string.Empty).ToList()
            : global::System.Array.Empty<string>();
        IsSuggestion = o.CandidateWindow != null
            && o.CandidateWindow.Category == Pb.Category.Suggestion;
    }

    public void Shutdown()
    {
        if (_hasSession)
        {
            Send(new Pb.Input { Type = Pb.Input.Types.CommandType.DeleteSession, Id = _sessionId });
            _hasSession = false;
        }
    }
}
