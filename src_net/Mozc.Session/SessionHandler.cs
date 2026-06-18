using Mozc.Engine;
using Mozc.Rewriter;

namespace Mozc.Session;

// C++ src/session/session_handler.cc の中核スライス。複数 Session のプールを管理し、
// Input(CreateSession/DeleteSession/SendKey)を評価して Output を返す。
// session id 採番・上限・LRU 破棄・config/Reload は簡略(後続)。
public sealed class SessionHandler
{
    private readonly MozcEngine _engine;
    private readonly KeyMap _keyMap;
    private readonly IRewriter? _rewriter;
    // 履歴予測はユーザ単位で全セッション共有(C++ も UserHistoryPredictor は engine 共有)。
    private readonly Prediction.UserHistoryPredictor _history = new();
    private readonly Dictionary<ulong, Session> _sessions = new();
    private ulong _nextId = 1;

    public const int MaxSessions = 64;

    public SessionHandler(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null)
    {
        _engine = engine;
        _keyMap = keyMap;
        _rewriter = rewriter;
    }

    public Prediction.UserHistoryPredictor History => _history;

    // 起動時に履歴 db を読み込む(無ければ何もしない)。
    public bool LoadHistory(string path) => Prediction.UserHistoryStorage.LoadFile(_history, path);

    // 終了時/定期に履歴 db を書き出す。
    public void SaveHistory(string path) => Prediction.UserHistoryStorage.Save(_history, path);

    public int SessionCount => _sessions.Count;

    public Output EvalCommand(Input input)
    {
        switch (input.Type)
        {
            case CommandType.CreateSession:
                return CreateSession();
            case CommandType.DeleteSession:
                return DeleteSession(input.SessionId);
            case CommandType.SendKey:
                return SendKey(input);
            case CommandType.SendCommand:
                return SendCommand(input);
            case CommandType.NoOperation:
                return new Output { SessionId = input.SessionId, Consumed = true };
            default:
                return new Output { ErrorOccured = true };
        }
    }

    private Output CreateSession()
    {
        if (_sessions.Count >= MaxSessions)
        {
            return new Output { ErrorOccured = true };
        }
        ulong id = _nextId++;
        _sessions[id] = new Session(_engine, _keyMap, _rewriter, _history);
        return new Output { SessionId = id, Consumed = true };
    }

    private Output DeleteSession(ulong id)
    {
        bool removed = _sessions.Remove(id);
        return new Output { SessionId = id, Consumed = removed, ErrorOccured = !removed };
    }

    private Output SendCommand(Input input)
    {
        if (!_sessions.TryGetValue(input.SessionId, out Session? session))
        {
            return new Output { SessionId = input.SessionId, ErrorOccured = true };
        }
        SessionResult r = session.SendCommand(input.SessionCommand, input.CommandId);
        return ToOutput(input.SessionId, session, r);
    }

    private Output SendKey(Input input)
    {
        if (!_sessions.TryGetValue(input.SessionId, out Session? session))
        {
            return new Output { SessionId = input.SessionId, ErrorOccured = true };
        }
        SessionResult r = input.Key != null
            ? session.SendKey(input.Key)
            : session.SendKey(input.KeyString);
        return ToOutput(input.SessionId, session, r);
    }

    private static Output ToOutput(ulong sessionId, Session session, SessionResult r)
    {
        return new Output
        {
            SessionId = sessionId,
            Consumed = r.Consumed,
            Preedit = r.Preedit,
            Result = r.Committed,
            Candidates = session.Converter.GetCandidates(),
        };
    }
}
