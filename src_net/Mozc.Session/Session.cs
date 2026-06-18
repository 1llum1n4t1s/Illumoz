using Mozc.Engine;
using Mozc.Rewriter;

namespace Mozc.Session;

// キー入力 1 回の処理結果。
public sealed class SessionResult
{
    public string Committed { get; init; } = string.Empty; // この入力で確定した文字列
    public string Preedit { get; init; } = string.Empty;   // 現在の未確定文字列
    public bool Consumed { get; init; }                     // IME がキーを消費したか
}

// C++ src/session/session.cc の中核スライス。KeyMap で KeyEvent→command を引き、
// SessionConverter を操作してキー駆動の変換セッションを実現する。
// commands.proto(Output/Candidates)生成・全 command 網羅は後続。
public sealed class Session
{
    private readonly KeyMap _keyMap;
    private readonly SessionConverter _converter;
    // Backspace 用に打鍵列を保持(Composer は編集 API 未実装のため再構築する)。
    private readonly List<string> _typed = new();
    // Undo 用: 直前に確定した打鍵列(確定の取り消しで composition を復元)。
    private List<string> _lastCommitted = new();

    // 確定時に打鍵列を Undo 用へ退避してクリアする。
    private void SnapshotAndClearTyped()
    {
        if (_typed.Count > 0)
        {
            _lastCommitted = new List<string>(_typed);
        }
        _typed.Clear();
    }

    // 直前の確定を取り消し、確定前の composition を復元する(C++ Undo 相当)。
    public SessionResult Undo()
    {
        if (_lastCommitted.Count == 0)
        {
            return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
        _converter.Reset();
        _typed.Clear();
        foreach (string ch in _lastCommitted)
        {
            _converter.InsertCharacter(ch);
            _typed.Add(ch);
        }
        _lastCommitted = new List<string>();
        return Current(true);
    }

    public Session(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null,
        Prediction.UserHistoryPredictor? history = null,
        Dictionary.UserDictionaryStorage? userDict = null)
    {
        _keyMap = keyMap;
        _converter = new SessionConverter(engine, rewriter, history, userDict);
    }

    public SessionConverter Converter => _converter;
    public string GetPreedit() => _converter.GetPreedit();

    // 入力中(composition)のサジェスト候補(履歴+辞書統合)。確定/変換中は空。
    public IReadOnlyList<string> GetSuggestions(int maxResults = 9)
    {
        if (_converter.CurrentState != SessionConverter.State.Composition || _typed.Count == 0)
        {
            return global::System.Array.Empty<string>();
        }
        return _converter.PredictMerged(maxResults).ConvertAll(p => p.Value);
    }

    // 入力前(打鍵なし)のゼロクエリ候補(履歴の直近)。入力中/変換中は空。
    public IReadOnlyList<string> GetZeroQuerySuggestions(int maxResults = 5)
    {
        if (_converter.CurrentState != SessionConverter.State.Composition || _typed.Count != 0)
        {
            return global::System.Array.Empty<string>();
        }
        return _converter.PredictZeroQuery(maxResults).ConvertAll(p => p.Value);
    }

    private string Status() => _converter.CurrentState switch
    {
        SessionConverter.State.Conversion => "Conversion",
        _ => _typed.Count == 0 ? "Precomposition" : "Composition",
    };

    public SessionResult SendKey(KeyEvent key)
    {
        string status = Status();
        string? command = _keyMap.GetCommand(status, key);

        if (command != null)
        {
            return Dispatch(command, key);
        }

        // command 未定義: 印字可能な文字なら入力として扱う。
        if (key.Special == null && key.KeyCode is int code && code >= 0x20
            && !key.Modifiers.Contains(ModifierKey.Ctrl) && !key.Modifiers.Contains(ModifierKey.Alt))
        {
            return InsertChar(char.ConvertFromUtf32(code));
        }
        return new SessionResult { Preedit = GetPreedit(), Consumed = false };
    }

    // SEND_COMMAND: 候補の明示選択・確定・取消。
    public SessionResult SendCommand(SessionCommandType type, int id)
    {
        switch (type)
        {
            case SessionCommandType.SelectCandidate:
            case SessionCommandType.HighlightCandidate:
                _converter.SelectCandidate(id);
                return Current(true);
            case SessionCommandType.SubmitCandidate:
                // 入力中(サジェスト)はサジェスト候補を直接確定する。
                if (_converter.CurrentState == SessionConverter.State.Composition)
                {
                    string? sug = _converter.CommitSuggestion(id);
                    if (sug != null)
                    {
                        SnapshotAndClearTyped();
                        return new SessionResult { Committed = sug, Preedit = "", Consumed = true };
                    }
                }
                _converter.SelectCandidate(id);
                goto case SessionCommandType.Submit;
            case SessionCommandType.Submit:
            {
                string committed = _converter.Commit();
                SnapshotAndClearTyped();
                return new SessionResult { Committed = committed, Preedit = "", Consumed = true };
            }
            case SessionCommandType.Revert:
                if (_converter.CurrentState == SessionConverter.State.Conversion)
                {
                    _converter.Cancel();
                }
                else
                {
                    _converter.Reset();
                    _typed.Clear();
                }
                return Current(true);
            default:
                return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
    }

    public SessionResult SendKey(string keyString)
        => KeyParser.TryParse(keyString, out KeyEvent ke)
            ? SendKey(ke)
            : new SessionResult { Preedit = GetPreedit(), Consumed = false };

    private SessionResult Dispatch(string command, KeyEvent key)
    {
        switch (command)
        {
            case "Commit":
            {
                string committed = _converter.Commit();
                SnapshotAndClearTyped();
                return new SessionResult { Committed = committed, Preedit = "", Consumed = true };
            }
            case "Convert":
                _converter.Convert();
                return Current(true);
            case "ConvertNext":
            case "ConvertNextPage":
                // 未変換なら変換開始、変換中なら次候補(スペース挙動)。
                if (_converter.CurrentState == SessionConverter.State.Composition)
                {
                    _converter.Convert();
                }
                else
                {
                    _converter.ConvertNext();
                }
                return Current(true);
            case "ConvertPrev":
            case "ConvertPrevPage":
                _converter.ConvertPrev();
                return Current(true);
            case "SegmentFocusRight":
            case "SegmentFocusLast":
                _converter.SegmentFocusRight();
                return Current(true);
            case "SegmentFocusLeft":
            case "SegmentFocusFirst":
                _converter.SegmentFocusLeft();
                return Current(true);
            case "Cancel":
                if (_converter.CurrentState == SessionConverter.State.Conversion)
                {
                    _converter.Cancel();
                }
                else
                {
                    _converter.Reset();
                    _typed.Clear();
                }
                return Current(true);
            case "Backspace":
                return Backspace();
            default:
                // 未対応 command はキーを消費しない。
                return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
    }

    private SessionResult InsertChar(string ch)
    {
        bool wasConversion = _converter.CurrentState == SessionConverter.State.Conversion;
        string committed = string.Empty;
        if (wasConversion)
        {
            // 変換中の入力 → SessionConverter 側で直前確定。打鍵列はリセット。
            committed = _converter.Commit();
            SnapshotAndClearTyped();
        }
        _converter.InsertCharacter(ch);
        _typed.Add(ch);
        return new SessionResult { Committed = committed, Preedit = GetPreedit(), Consumed = true };
    }

    private SessionResult Backspace()
    {
        if (_converter.CurrentState == SessionConverter.State.Conversion)
        {
            _converter.Cancel();
            return Current(true);
        }
        if (_typed.Count == 0)
        {
            return new SessionResult { Preedit = "", Consumed = false };
        }
        _typed.RemoveAt(_typed.Count - 1);
        _converter.Reset();
        foreach (string ch in _typed)
        {
            _converter.InsertCharacter(ch);
        }
        return Current(true);
    }

    private SessionResult Current(bool consumed)
        => new() { Preedit = GetPreedit(), Consumed = consumed };
}
