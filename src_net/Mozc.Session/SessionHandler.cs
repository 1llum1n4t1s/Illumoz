using Mozc.Engine;
using Mozc.Rewriter;

namespace Mozc.Session;

// C++ src/session/session_handler.cc の中核スライス。複数 Session のプールを管理し、
// Input(CreateSession/DeleteSession/SendKey)を評価して Output を返す。
// session id 採番・上限・LRU 破棄・config/Reload は簡略(後続)。
public sealed class SessionHandler
{
    private readonly MozcEngine _engine;
    private KeyMap _keyMap;
    private readonly IRewriter? _rewriter;
    // 履歴予測はユーザ単位で全セッション共有(C++ も UserHistoryPredictor は engine 共有)。
    private readonly Prediction.UserHistoryPredictor _history = new();
    // ユーザー辞書もユーザ単位で全セッション共有。
    private readonly Dictionary.UserDictionaryStorage _userDict = new();
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
    public Dictionary.UserDictionaryStorage UserDictionary => _userDict;
    public MozcEngine Engine => _engine;
    public IRewriter? Rewriter => _rewriter;

    // keymap を差し替える(以降の新規セッションに反映。設定変更時に EngineServer から呼ぶ)。
    public void SetKeyMap(KeyMap keyMap) => _keyMap = keyMap;
    public KeyMap KeyMap => _keyMap;

    // 起動時に履歴 db を読み込む(無ければ何もしない)。
    public bool LoadHistory(string path) => Prediction.UserHistoryStorage.LoadFile(_history, path);

    // 終了時/定期に履歴 db を書き出す。
    public void SaveHistory(string path) => Prediction.UserHistoryStorage.Save(_history, path);

    // ユーザー辞書 db の読込/保存(無ければ何もしない)。
    public bool LoadUserDictionary(string path) => _userDict.LoadFile(path);
    public void SaveUserDictionary(string path) => _userDict.Save(path);

    // WordRegister GUI 相当: 単語を登録する(以降の変換/予測に即反映)。
    public bool RegisterWord(string reading, string word, string pos = "名詞", string comment = "")
        => _userDict.Add(new Dictionary.UserDictionaryStorage.UserEntry(reading, word, pos, comment));

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
            case CommandType.ClearUserHistory:
            case CommandType.ClearUserPrediction:
                // 履歴/予測の学習を実際に消去する(黙って成功扱いにしない)。
                _history.Clear();
                return new Output { SessionId = input.SessionId, Consumed = true };
            case CommandType.ClearUnusedUserPrediction:
            case CommandType.Reload:
            case CommandType.SyncData:
                // 受理(永続化のフラッシュ/設定リロードはプロファイル層が担う)。
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
        _sessions[id] = new Session(_engine, _keyMap, _rewriter, _history, _userDict);
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
            CandidateDescriptions = session.Converter.GetCandidateDescriptions(),
            Suggestions = session.GetSuggestions(),
        };
    }
}
