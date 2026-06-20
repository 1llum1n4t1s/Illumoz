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
    // LRU 順(先頭=最近使用 / 末尾=最古)。上限到達時に末尾を破棄する(C++ の
    // DeleteSessionId による最古セッション破棄に相当)。
    private readonly LinkedList<ulong> _lru = new();
    private ulong _nextId = 1;
    // 全セッション共有の挙動設定(EngineServer.ApplyConfig が更新)。
    private readonly SessionSettings _settings = new();

    public const int MaxSessions = 64;

    public SessionSettings Settings => _settings;

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

    // keymap を差し替える。新規セッションだけでなく、生成済みの全セッションへも反映する
    // (クライアントはセッションを使い回すため、既存に伝播しないと設定変更が効かない)。
    public void SetKeyMap(KeyMap keyMap)
    {
        _keyMap = keyMap;
        foreach (Session s in _sessions.Values)
        {
            s.SetKeyMap(keyMap);
        }
    }
    public KeyMap KeyMap => _keyMap;

    // ローマ字表変更後、idle な既存セッションの composer を最新表で作り直す。
    public void RefreshIdleComposers()
    {
        foreach (Session s in _sessions.Values)
        {
            s.RefreshComposerIfIdle();
        }
    }

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
            case CommandType.TestSendKey:
                return TestSendKey(input);
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
        // 上限到達時はエラーにせず、最も長く使われていないセッションを破棄して空きを作る
        // (C++ session_handler は最古セッションを消して新規生成を継続する)。
        if (_sessions.Count >= MaxSessions && _lru.Last != null)
        {
            ulong oldest = _lru.Last.Value;
            _sessions.Remove(oldest);
            _lru.RemoveLast();
        }
        ulong id = _nextId++;
        _sessions[id] = new Session(_engine, _keyMap, _rewriter, _history, _userDict, _settings);
        _lru.AddFirst(id);
        return new Output { SessionId = id, Consumed = true };
    }

    private Output DeleteSession(ulong id)
    {
        bool removed = _sessions.Remove(id);
        if (removed)
        {
            _lru.Remove(id);
        }
        return new Output { SessionId = id, Consumed = removed, ErrorOccured = !removed };
    }

    // セッション利用時に LRU 先頭へ移動する(最近使用としてマーク)。
    private void Touch(ulong id)
    {
        if (_lru.First?.Value == id)
        {
            return;
        }
        _lru.Remove(id);
        _lru.AddFirst(id);
    }

    private Output SendCommand(Input input)
    {
        if (!_sessions.TryGetValue(input.SessionId, out Session? session))
        {
            return new Output { SessionId = input.SessionId, ErrorOccured = true };
        }
        Touch(input.SessionId);
        SessionResult r = session.SendCommand(input.SessionCommand, input.CommandId);
        return ToOutput(input.SessionId, session, r, input.SuppressSuggestion);
    }

    // TEST_SEND_KEY: 状態を変えず消費可否のみ判定する。
    private Output TestSendKey(Input input)
    {
        if (!_sessions.TryGetValue(input.SessionId, out Session? session))
        {
            return new Output { SessionId = input.SessionId, ErrorOccured = true };
        }
        Touch(input.SessionId);
        // SEND_KEY と同じ判定で、key_string 付きの生テキスト(かな/ソフトキーボード/TEXT_INPUT)は
        // テキスト挿入経路の消費可否を返す。さもないと Key だけ見て横取り不可と誤判定し、
        // クライアントがテキストを IME を素通しさせてしまう。
        SessionResult r;
        if (PreferKeyString(input, session))
        {
            // input_style=DIRECT_INPUT は precomposition で echo back 扱い(未消費)になる。
            r = input.Key!.InputStyle == InputStyle.DirectInput
                ? session.TestInsertTextDirect(input.KeyString, input.Key.KeyCode)
                : session.TestInsertText(input.KeyString, input.Key.Activated);
        }
        else if (input.Key != null)
        {
            r = session.TestSendKey(input.Key);
        }
        else
        {
            r = session.TestSendKey(input.KeyString);
        }
        return ToOutput(input.SessionId, session, r, input.SuppressSuggestion);
    }

    // key_string を「生テキスト」として composer へ入れるべき入力かを判定する(SEND_KEY/TEST_SEND_KEY 共通)。
    // 直接入力(IME off)中は素通しさせるため false。特殊/修飾キー付きは keymap 解釈に回す。
    private static bool PreferKeyString(Input input, Session session)
    {
        bool isTextInput = input.Key?.Special == SpecialKey.TextInput;
        // クライアントが activated=false を明示したキーは IME off 扱い。SendKey が状態を同期する
        // 前にここで生テキスト経路へ入ると素通しできないため、現状態に加えてこのフラグも見る。
        bool active = input.Key?.Activated ?? session.Activated;
        return input.KeyString.Length > 0 && input.Key != null && active
            && (isTextInput || (input.Key.Special == null && input.Key.Modifiers.Count == 0));
    }

    private Output SendKey(Input input)
    {
        if (!_sessions.TryGetValue(input.SessionId, out Session? session))
        {
            return new Output { SessionId = input.SessionId, ErrorOccured = true };
        }
        Touch(input.SessionId);
        // key_string が付いた直接入力(かな入力/ソフトキーボード/TEXT_INPUT)は、特殊キーや
        // 修飾キーを伴わない限り key_string をそのまま「生テキスト」として composer へ入れる。
        // key_code は ASCII フォールバックに過ぎず、優先すると "ぱ" 等の合成文字を取りこぼすため。
        // ただし Key 自体が無い(KeyString だけ渡された)入力は、KeyString を keymap 構文
        // (例 "Space" / "Ctrl a")として解釈する従来パスを使う(protobuf では key_string は
        // 必ず Key の一部として届くため、この分岐に来るのは文字列キー指定の経路)。
        SessionResult r;
        if (PreferKeyString(input, session))
        {
            // 間接 IME-ON/OFF をディスパッチ前に同期する(SendKey(KeyEvent) と判定 source を揃える)。
            session.SyncIndirectImeOnOff(input.Key!.Activated);
            // input_style=DIRECT_INPUT の key_string は precomposition で即時確定する(直接入力)。
            // AS_IS はローマ字変換せず literal を保持する。それ以外は生テキストとして composer へ入れる。
            r = input.Key!.InputStyle switch
            {
                InputStyle.DirectInput => session.InsertTextDirect(input.KeyString, input.Key.KeyCode),
                InputStyle.AsIs => session.InsertTextAsIs(input.KeyString),
                _ => session.InsertText(input.KeyString),
            };
        }
        else if (input.Key != null)
        {
            r = session.SendKey(input.Key);
        }
        else
        {
            r = session.SendKey(input.KeyString);
        }
        return ToOutput(input.SessionId, session, r, input.SuppressSuggestion);
    }

    private static Output ToOutput(ulong sessionId, Session session, SessionResult r,
        bool suppressSuggestion = false)
    {
        return new Output
        {
            SessionId = sessionId,
            Consumed = r.Consumed,
            Preedit = r.Preedit,
            Result = r.Committed,
            Candidates = session.Converter.GetCandidates(),
            CandidateDescriptions = session.Converter.GetCandidateDescriptions(),
            // クライアントが抑止を要求した応答ではサジェストを出さない。
            Suggestions = suppressSuggestion
                ? global::System.Array.Empty<string>()
                : session.GetSuggestions(),
            FocusedIndex = session.Converter.FocusedCandidateIndex,
            FocusedPosition = session.Converter.FocusedPosition,
            ConverterCommand = session.Converter.TakeLastCommand(),
            Activated = session.Activated,
        };
    }
}
