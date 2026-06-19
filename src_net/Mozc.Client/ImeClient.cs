using Google.Protobuf;
using Pb = Mozc.Commands;

namespace Mozc.Client;

// IME クライアント側の表示状態(全 OS 統合層が共有)。
public sealed class ImeState
{
    public string Preedit { get; init; } = string.Empty;
    public string Commit { get; init; } = string.Empty;
    public IReadOnlyList<string> Candidates { get; init; } = global::System.Array.Empty<string>();
    // 候補のショートカット文字(SelectionShortcut。候補と同数か空)。
    public IReadOnlyList<string> Shortcuts { get; init; } = global::System.Array.Empty<string>();
    // 候補の説明(記号/全角 等。候補と同数か空)。
    public IReadOnlyList<string> Descriptions { get; init; } = global::System.Array.Empty<string>();
    // 候補窓が変換候補ではなく入力中サジェストか(C++ category=SUGGESTION)。
    public bool IsSuggestion { get; init; }
    public bool Consumed { get; init; }
}

// C++ src/client/client.cc 相当(OS 非依存)。protobuf(commands.proto)で mozc_server と会話。
// transport は Func<byte[],byte[]>(proto Input → proto Output。実体=NamedPipe/Unix client、
// テスト=in-proc EngineServer.HandleProtoRequest)。TSF/IMK/ibus 各層が共有する。
public sealed class ImeClient
{
    private readonly Func<byte[], byte[]> _transport;
    private ulong _sessionId;
    private bool _hasSession;

    public ImeClient(Func<byte[], byte[]> transport) => _transport = transport;

    private Pb.Output Send(Pb.Input input) => Pb.Output.Parser.ParseFrom(_transport(input.ToByteArray()));

    public void EnsureSession()
    {
        if (_hasSession)
        {
            return;
        }
        // session id=0 はサーバの作成失敗(上限到達等)。確立扱いにせず次回再試行する。
        ulong id = Send(new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;
        if (id != 0)
        {
            _sessionId = id;
            _hasSession = true;
        }
    }

    public ImeState SendCharacter(char c) => SendKey(new Pb.KeyEvent { KeyCode = c });

    public ImeState SendSpecialKey(Pb.KeyEvent.Types.SpecialKey special)
        => SendKey(new Pb.KeyEvent { SpecialKey = special });

    // 直近の応答状態(ショートカット選択の参照用)。
    public ImeState LastState { get; private set; } = new();

    public ImeState SendKey(Pb.KeyEvent key)
    {
        EnsureSession();
        Pb.Output o = Send(new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = _sessionId,
            Key = key,
        });
        return LastState = ToState(o);
    }

    // 候補の明示選択+確定(SUBMIT_CANDIDATE)。
    public ImeState SubmitCandidate(int index)
    {
        EnsureSession();
        Pb.Output o = Send(new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendCommand,
            Id = _sessionId,
            Command = new Pb.SessionCommand
            {
                Type = Pb.SessionCommand.Types.CommandType.SubmitCandidate,
                Id = index,
            },
        });
        return LastState = ToState(o);
    }

    // ショートカット文字(候補窓の '1'..'9' 等)で確定する。直近状態の Shortcuts で
    // index を解決(該当無しは現状態を返すだけ)。
    public ImeState SubmitByShortcut(char shortcut)
    {
        for (int i = 0; i < LastState.Shortcuts.Count; i++)
        {
            if (LastState.Shortcuts[i].Length == 1 && LastState.Shortcuts[i][0] == shortcut)
            {
                return SubmitCandidate(i);
            }
        }
        return LastState;
    }

    public void Shutdown()
    {
        if (_hasSession)
        {
            Send(new Pb.Input { Type = Pb.Input.Types.CommandType.DeleteSession, Id = _sessionId });
            _hasSession = false;
        }
    }

    private static ImeState ToState(Pb.Output o) => new()
    {
        Consumed = o.Consumed,
        Commit = o.Result != null ? o.Result.Value : string.Empty,
        Preedit = o.Preedit != null && o.Preedit.Segment.Count > 0 ? o.Preedit.Segment[0].Value : string.Empty,
        Candidates = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Value).ToList()
            : global::System.Array.Empty<string>(),
        Shortcuts = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Annotation?.Shortcut ?? string.Empty).ToList()
            : global::System.Array.Empty<string>(),
        Descriptions = o.CandidateWindow != null
            ? o.CandidateWindow.Candidate.Select(c => c.Annotation?.Description ?? string.Empty).ToList()
            : global::System.Array.Empty<string>(),
        IsSuggestion = o.CandidateWindow != null
            && o.CandidateWindow.Category == Pb.Category.Suggestion,
    };
}
