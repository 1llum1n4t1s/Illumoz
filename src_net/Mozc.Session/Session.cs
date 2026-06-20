using Mozc.Engine;
using Mozc.Rewriter;

namespace Mozc.Session;

// キー入力 1 回の処理結果。
public sealed class SessionResult
{
    public string Committed { get; init; } = string.Empty; // この入力で確定した文字列
    public string Preedit { get; init; } = string.Empty;   // 現在の未確定文字列
    public bool Consumed { get; init; }                     // IME がキーを消費したか
    // 確定したコマンド候補のコマンド(incognito/presentation トグル等。無ければ Default)。
    public Mozc.Converter.Candidate.CommandType Command { get; init; }
        = Mozc.Converter.Candidate.CommandType.DefaultCommand;
}

// C++ src/session/session.cc の中核スライス。KeyMap で KeyEvent→command を引き、
// SessionConverter を操作してキー駆動の変換セッションを実現する。
// commands.proto(Output/Candidates)生成・全 command 網羅は後続。
public sealed class Session
{
    private KeyMap _keyMap;
    private readonly SessionConverter _converter;
    // IME が有効(変換受付)か。false は直接入力(DirectInput)状態。
    private bool _activated = true;
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
        // 確定で履歴が更新され得るので、サジェスト有無キャッシュを無効化する
        // (同じ読みを再入力したとき新しい履歴候補を取りこぼさない)。
        _suggestionCacheKey = null;
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

    private readonly SessionSettings _settings;

    public Session(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null,
        Prediction.UserHistoryPredictor? history = null,
        Dictionary.UserDictionaryStorage? userDict = null,
        SessionSettings? settings = null)
    {
        _keyMap = keyMap;
        _converter = new SessionConverter(engine, rewriter, history, userDict);
        _settings = settings ?? new SessionSettings();
    }

    public SessionConverter Converter => _converter;
    public string GetPreedit() => _converter.GetPreedit();

    // IME 有効状態(クライアントへ Status.activated として返す)。
    public bool Activated => _activated;

    // keymap を差し替える(SET_CONFIG で keymap が変わったとき既存セッションへ反映する)。
    public void SetKeyMap(KeyMap keyMap) => _keyMap = keyMap;

    // idle なら engine の最新ローマ字表で composer を作り直す(roman_table 設定変更の反映)。
    public void RefreshComposerIfIdle() => _converter.RefreshComposerIfIdle();

    // 入力中(composition)のサジェスト候補(履歴+辞書統合)。確定/変換中は空。
    public IReadOnlyList<string> GetSuggestions(int maxResults = 9)
    {
        // サジェスト無効(use_history/dictionary/realtime 全 off)なら出さない。
        if (!_settings.SuggestionEnabled
            || _converter.CurrentState != SessionConverter.State.Composition || _typed.Count == 0)
        {
            return global::System.Array.Empty<string>();
        }
        // config.suggestions_size でクランプ(0 なら出さない)。
        int limit = global::System.Math.Min(maxResults, _settings.SuggestionSize);
        if (limit <= 0)
        {
            return global::System.Array.Empty<string>();
        }
        // シークレット/履歴無効では履歴由来を出さない。辞書/リアルタイム無効では辞書由来を出さない。
        return _converter.PredictMerged(
                limit,
                includeHistory: _settings.UseHistorySuggest && !_settings.IncognitoMode,
                includeDictionary: _settings.UseDictionarySuggest || _settings.UseRealtimeConversion)
            .ConvertAll(p => p.Value);
    }

    // 入力前(打鍵なし)のゼロクエリ候補(履歴の直近)。入力中/変換中は空。
    public IReadOnlyList<string> GetZeroQuerySuggestions(int maxResults = 5)
    {
        // ゼロクエリは履歴由来。サジェスト無効/シークレットでは出さない。
        if (!_settings.SuggestionEnabled || _settings.IncognitoMode
            || _converter.CurrentState != SessionConverter.State.Composition || _typed.Count != 0)
        {
            return global::System.Array.Empty<string>();
        }
        return _converter.PredictZeroQuery(maxResults).ConvertAll(p => p.Value);
    }

    // サジェスト有無の判定キャッシュ(現在の preedit 文字列でメモ化)。Status() は
    // キー入力ごと(TEST_SEND_KEY + SEND_KEY で 2 回)呼ばれるため、毎回 PredictMerged を
    // 走らせないよう preedit が変わらない限り前回結果を使う。
    private string? _suggestionCacheKey;
    private bool _suggestionCacheValue;

    // 入力中(composition)でサジェストが出ているか。Suggestion キーマップ状態の判定に使う。
    private bool HasActiveSuggestion()
    {
        if (!_settings.SuggestionEnabled
            || _converter.CurrentState != SessionConverter.State.Composition
            || _typed.Count == 0)
        {
            return false;
        }
        string key = GetPreedit();
        if (key != _suggestionCacheKey)
        {
            _suggestionCacheKey = key;
            _suggestionCacheValue = _converter.PredictMerged(
                1,
                includeHistory: _settings.UseHistorySuggest && !_settings.IncognitoMode,
                includeDictionary: _settings.UseDictionarySuggest || _settings.UseRealtimeConversion)
                .Count > 0;
        }
        return _suggestionCacheValue;
    }

    // keymap 照合用の状態名。サジェスト表示中は "Suggestion"(CommitFirstSuggestion 等の
    // Suggestion 固有バインドを到達可能にする。未該当キーは Composition 行へフォールバック)。
    private string Status()
    {
        if (!_activated)
        {
            return "DirectInput"; // IME off。IMEOn 以外は素通し。
        }
        return _converter.CurrentState switch
        {
            SessionConverter.State.Conversion => "Conversion",
            _ => _typed.Count == 0 ? "Precomposition"
                : HasActiveSuggestion() ? "Suggestion" : "Composition",
        };
    }

    public SessionResult SendKey(KeyEvent key)
    {
        string status = Status();
        string? command = _keyMap.GetCommand(status, key);

        if (command != null)
        {
            return Dispatch(command, key);
        }

        // 直接入力(IME off)中は IMEOn 以外を一切消費しない(アプリへ素通し)。
        if (!_activated)
        {
            return new SessionResult { Preedit = string.Empty, Consumed = false };
        }

        // 選択ショートカット(既定 "123456789" 等)が設定されていて、印字キーがその文字なら、
        // 通常入力の前に候補確定として扱う(候補窓に出した shortcut を SEND_KEY 経路でも機能させる)。
        if (_settings.SelectionShortcuts.Length > 0
            && key.Special == null && key.KeyCode is int sc && sc <= 0x7F
            && !key.Modifiers.Contains(ModifierKey.Ctrl) && !key.Modifiers.Contains(ModifierKey.Alt))
        {
            int idx = _settings.SelectionShortcuts.IndexOf((char)sc);
            if (idx >= 0)
            {
                // 変換中: 注目文節の候補 N を選んで確定。
                if (_converter.CurrentState == SessionConverter.State.Conversion
                    && _converter.SelectByShortcut((char)sc, _settings.SelectionShortcuts))
                {
                    string committed = _converter.Commit();
                    SnapshotAndClearTyped();
                    return new SessionResult { Committed = committed, Preedit = "", Consumed = true };
                }
                // サジェスト表示中: サジェスト候補 N を直接確定。
                if (HasActiveSuggestion())
                {
                    string? sug = _converter.CommitSuggestion(idx, includeHistory: !_settings.IncognitoMode);
                    if (sug != null)
                    {
                        SnapshotAndClearTyped();
                        return new SessionResult { Committed = sug, Preedit = "", Consumed = true };
                    }
                }
            }
        }

        // command 未定義: 印字可能な文字なら入力として扱う。
        if (key.Special == null && key.KeyCode is int code && code >= 0x20
            && !key.Modifiers.Contains(ModifierKey.Ctrl) && !key.Modifiers.Contains(ModifierKey.Alt))
        {
            return InsertChar(char.ConvertFromUtf32(code));
        }
        return new SessionResult { Preedit = GetPreedit(), Consumed = false };
    }

    // TEST_SEND_KEY: セッション状態を変えずに「このキーを消費するか」だけを判定する。
    // IME 側が前処理でキーを横取りすべきか決めるために使う(C++ session TestSendKey 相当)。
    public SessionResult TestSendKey(KeyEvent key)
    {
        string status = Status();
        // 直接入力(IME off)中は印字キーを消費しない。SendKey 側のガードと一致させ、
        // クライアントが横取り判定を誤らないようにする(IMEOn 等のコマンドのみ消費)。
        bool consumed = _keyMap.GetCommand(status, key) != null
            || (_activated && key.Special == null && key.KeyCode is int code && code >= 0x20
                && !key.Modifiers.Contains(ModifierKey.Ctrl)
                && !key.Modifiers.Contains(ModifierKey.Alt));
        return new SessionResult { Preedit = GetPreedit(), Consumed = consumed };
    }

    public SessionResult TestSendKey(string keyString)
        => KeyParser.TryParse(keyString, out KeyEvent ke)
            ? TestSendKey(ke)
            : new SessionResult { Preedit = GetPreedit(), Consumed = false };

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
                    string? sug = _converter.CommitSuggestion(id, includeHistory: !_settings.IncognitoMode);
                    if (sug != null)
                    {
                        SnapshotAndClearTyped();
                        return new SessionResult { Committed = sug, Preedit = "", Consumed = true };
                    }
                }
                // 範囲外の候補 id では確定せずセッションを変えない(誤テキスト挿入の防止)。
                if (!_converter.SelectCandidate(id))
                {
                    return Current(false);
                }
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

    // key_string の直接入力(ソフトキーボード/TEXT_INPUT)。keymap 構文として再解釈せず、
    // 文字列をコードポイント単位でそのまま composer へ入れる。空白や複数コードポイントの
    // テキストが「解釈不能キー」として落ちるのを防ぐ。
    public SessionResult InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
        SessionResult last = Current(true);
        foreach (global::System.Text.Rune rune in text.EnumerateRunes())
        {
            last = InsertChar(rune.ToString());
        }
        return last;
    }

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
            case "CommitOnlyFirstSegment":
                // 第一文節のみ確定は未実装(文節分割編集が前提)。変換中は全体確定として
                // 消費し、キーがアプリへ抜けないようにする(単一文節では等価)。
                if (_converter.CurrentState == SessionConverter.State.Conversion)
                {
                    string committed = _converter.Commit();
                    SnapshotAndClearTyped();
                    return new SessionResult { Committed = committed, Preedit = "", Consumed = true };
                }
                return ConsumeNoOpWhile(_typed.Count > 0);
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
            case "Delete":
                // preedit がある間は前方削除コマンドを消費する(末尾カーソルでは no-op)。
                // 消費しないとアプリ側へ Delete が抜けて変換中に周辺文書が削れる。
                if (_converter.CurrentState == SessionConverter.State.Conversion)
                {
                    _converter.Cancel();
                    return Current(true);
                }
                return _typed.Count > 0
                    ? Current(true)
                    : new SessionResult { Preedit = "", Consumed = false };
            case "Undo":
                return Undo();
            case "CommitFirstSuggestion":
            {
                // サジェスト表示中に先頭候補を確定する(Shift Enter / Ctrl Enter)。
                string? sug = _converter.CommitSuggestion(0, includeHistory: !_settings.IncognitoMode);
                if (sug != null)
                {
                    SnapshotAndClearTyped();
                    return new SessionResult { Committed = sug, Preedit = "", Consumed = true };
                }
                return Current(true);
            }
            case "InsertSpace":
                // 設定の space_character_form に従う(precomposition の直接確定でも全角を尊重)。
                return InsertSpaceCommand(_settings.SpaceForm == SpaceForm.Full ? "　" : " ");
            case "InsertHalfSpace":
                return InsertSpaceCommand(" ");
            case "InsertFullSpace":
                return InsertSpaceCommand("　");
            case "InsertAlternateSpace":
                // 設定字形の逆(全角設定なら半角、半角設定なら全角)を挿入する。
                return InsertSpaceCommand(_settings.SpaceForm == SpaceForm.Full ? " " : "　");
            // KOTOERI プリセットは DisplayAs* 名で同じ表記変換を割り当てる(別名扱い)。
            case "ConvertToHiragana":
            case "DisplayAsHiragana":
                _converter.ConvertToTransliteration(c => c.GetHiragana());
                return Current(true);
            case "ConvertToFullKatakana":
            case "DisplayAsFullKatakana":
                _converter.ConvertToTransliteration(c => c.GetFullKatakana());
                return Current(true);
            case "ConvertToHalfKatakana":
            case "DisplayAsHalfKatakana":
            case "ConvertToHalfWidth":
                // 半角化(F8): かな読みは半角カタカナへ。
                _converter.ConvertToTransliteration(c => c.GetHalfKatakana());
                return Current(true);
            case "ConvertToFullAlphanumeric":
            case "DisplayAsFullAlphanumeric":
                _converter.ConvertToTransliteration(c => c.GetFullAscii());
                return Current(true);
            case "ConvertToHalfAlphanumeric":
            case "DisplayAsHalfAlphanumeric":
                _converter.ConvertToTransliteration(c => c.GetHalfAscii());
                return Current(true);
            case "PredictAndConvert":
                // サジェスト表示中の Down/Ctrl+Down。表示中の予測(履歴/辞書の前方一致語)を
                // 確定する。素の prefix を変換し直すと表示されていた候補を失うため。
                if (_converter.CurrentState == SessionConverter.State.Composition)
                {
                    if (HasActiveSuggestion())
                    {
                        string? sug = _converter.CommitSuggestion(
                            0, includeHistory: !_settings.IncognitoMode);
                        if (sug != null)
                        {
                            SnapshotAndClearTyped();
                            return new SessionResult { Committed = sug, Preedit = "", Consumed = true };
                        }
                    }
                    _converter.Convert();
                }
                else
                {
                    _converter.ConvertNext();
                }
                return Current(true);
            case "MoveCursorLeft":
            case "MoveCursorRight":
            case "MoveCursorToBeginning":
            case "MoveCursorToEnd":
            case "MoveCursorLeftByWord":
            case "MoveCursorRightByWord":
                // composer のカーソル編集は未実装。入力中/変換中は消費して、アプリ側の
                // キャレットが composition の外へ動くのを防ぐ(no-op)。
                return ConsumeNoOpWhile(_typed.Count > 0
                    || _converter.CurrentState == SessionConverter.State.Conversion);
            case "SegmentWidthShrink":
            case "SegmentWidthExpand":
                // 文節幅の伸縮は未実装。変換中は消費して、Shift+Left/Right 等がアプリへ抜けて
                // 周辺文書を選択/移動するのを防ぐ(no-op。実装は文節境界編集の follow-up)。
                return ConsumeNoOpWhile(_converter.CurrentState == SessionConverter.State.Conversion);
            case "IMEOn":
                _activated = true;
                return Current(true);
            case "IMEOff":
            case "CancelAndIMEOff":
            {
                // IMEOff: 入力中の preedit を確定してから無効化(かな入力後に OFF しても
                // 打鍵が失われないようにする)。CancelAndIMEOff: 確定せず破棄して無効化。
                string committed = string.Empty;
                if (command == "IMEOff"
                    && (_converter.CurrentState == SessionConverter.State.Conversion || _typed.Count > 0))
                {
                    committed = _converter.Commit();
                }
                _converter.Reset();
                _typed.Clear();
                _activated = false;
                return new SessionResult { Committed = committed, Preedit = string.Empty, Consumed = true };
            }
            default:
                // 未対応 command はキーを消費しない。
                return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
    }

    // スペース挿入コマンド。入力中(composition)なら打鍵として composer へ、
    // 入力前(precomposition)なら空白をそのまま確定する。いずれもキーを消費する
    // (アプリへ素のスペースを透過させない)。
    private SessionResult InsertSpaceCommand(string space)
    {
        if (_converter.CurrentState == SessionConverter.State.Composition && _typed.Count > 0)
        {
            return InsertChar(space);
        }
        if (_converter.CurrentState == SessionConverter.State.Conversion)
        {
            string committed = _converter.Commit();
            SnapshotAndClearTyped();
            return new SessionResult { Committed = committed + space, Preedit = "", Consumed = true };
        }
        return new SessionResult { Committed = space, Preedit = "", Consumed = true };
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

    // 未実装の編集コマンド(カーソル移動/文節幅)の共通処理。active(入力中/変換中)なら
    // no-op として消費し、そうでなければアプリへ素通しする。
    private SessionResult ConsumeNoOpWhile(bool active)
        => active ? Current(true) : new SessionResult { Preedit = GetPreedit(), Consumed = false };
}
