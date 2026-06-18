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
    private readonly Dictionary<ulong, Session> _sessions = new();
    private ulong _nextId = 1;

    public const int MaxSessions = 64;

    public SessionHandler(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null)
    {
        _engine = engine;
        _keyMap = keyMap;
        _rewriter = rewriter;
    }

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
        _sessions[id] = new Session(_engine, _keyMap, _rewriter);
        return new Output { SessionId = id, Consumed = true };
    }

    private Output DeleteSession(ulong id)
    {
        bool removed = _sessions.Remove(id);
        return new Output { SessionId = id, Consumed = removed, ErrorOccured = !removed };
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
        return new Output
        {
            SessionId = input.SessionId,
            Consumed = r.Consumed,
            Preedit = r.Preedit,
            Result = r.Committed,
            Candidates = session.Converter.GetCandidates(),
        };
    }
}
