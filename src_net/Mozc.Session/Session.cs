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
    // 打鍵 1 つ分の保持単位。文字に加え「AS_IS(ローマ字変換しない literal)で入れたか」を持つ。
    // Backspace の再構築や Undo の再生で、AS_IS で入れた literal をローマ字変換し直して
    // 壊さない(例: literal "ka" が一部削除で "か" にならない)ようにモードを保存する。
    private readonly record struct TypedKey(string Ch, bool AsIs);

    // Backspace 用に打鍵列を保持(Composer は編集 API 未実装のため再構築する)。
    private readonly List<TypedKey> _typed = new();
    // Undo 用: 直前に確定した打鍵列(確定の取り消しで composition を復元)。
    private List<TypedKey> _lastCommitted = new();

    // 打鍵列を composer へ再生する(各キーのモードに応じて romaji / AS_IS で投入)。
    private void ReplayTyped(IEnumerable<TypedKey> keys)
    {
        foreach (TypedKey k in keys)
        {
            if (k.AsIs)
            {
                _converter.InsertCharacterAsIs(k.Ch);
            }
            else
            {
                _converter.InsertCharacter(k.Ch);
            }
        }
    }

    // 確定時に打鍵列を Undo 用へ退避してクリアする。
    private void SnapshotAndClearTyped()
    {
        if (_typed.Count > 0)
        {
            _lastCommitted = new List<TypedKey>(_typed);
        }
        _typed.Clear();
        // 確定で履歴が更新され得るので、サジェスト有無キャッシュを無効化する
        // (同じ読みを再入力したとき新しい履歴候補を取りこぼさない)。
        _suggestionCacheKey = null;
    }

    // 部分確定(CommitHeadToFocusedSegments / CommitFirstSegment)後に Session 側の入力バッファを
    // 再同期する。_typed を残り読みへ、_lastCommitted を確定済み先頭読みへ揃えることで、
    // 後続の Backspace 再構築や Undo が古い全入力から既確定テキストを復活させないようにする。
    // 残り読みは composer を kana で再構築済み(CommitHeadSegments)なので、再生も romaji 経路
    // (AsIs=false)で composer と一致させる(かなは romaji 表で素通りする)。
    private void ResyncAfterPartialCommit()
    {
        _typed.Clear();
        foreach (global::System.Text.Rune r in _converter.ComposerReading.EnumerateRunes())
        {
            _typed.Add(new TypedKey(r.ToString(), false));
        }
        // Undo は直前確定の読みを composition へ戻す。部分確定では先頭側の読みを記録する。
        _lastCommitted = new List<TypedKey>();
        foreach (global::System.Text.Rune r in _converter.LastHeadReading.EnumerateRunes())
        {
            _lastCommitted.Add(new TypedKey(r.ToString(), false));
        }
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
        ReplayTyped(_lastCommitted); // 確定時のモード(romaji/AS_IS)を保って復元する。
        _typed.AddRange(_lastCommitted);
        _lastCommitted = new List<TypedKey>();
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

    // サジェストの表示と確定で同じソース可否を使うためのヘルパ(食い違うと未表示の
    // 履歴候補を確定してしまう)。
    private bool IncludeHistorySuggest => _settings.UseHistorySuggest && !_settings.IncognitoMode;
    private bool IncludeDictionarySuggest => _settings.UseDictionarySuggest || _settings.UseRealtimeConversion;

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
                includeHistory: IncludeHistorySuggest,
                includeDictionary: IncludeDictionarySuggest)
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
    // このリクエストでサジェストを抑止するか(context.suppress_suggestion / request_suggestion=false)。
    // SessionHandler が SendKey/TestSendKey/SendCommand の直前に設定する。出力から消すだけでなく、
    // ディスパッチ前の Status/keymap 判定でも非表示にし、画面に出ていない予測候補を
    // CommitFirstSuggestion/PredictAndConvert/選択ショートカットで確定しないようにする。
    private bool _suppressSuggestion;

    public void SetSuggestionSuppressed(bool suppress) => _suppressSuggestion = suppress;

    // パスワード欄など秘匿入力中は、確定テキストを共有ユーザー履歴へ学習させない
    // (後続の候補窓に秘密が出るのを防ぐ)。SessionHandler がリクエスト毎に設定する。
    public void SetPrivateMode(bool isPrivate) => _converter.LearningSuppressed = isPrivate;

    private bool HasActiveSuggestion()
    {
        if (_suppressSuggestion
            || !_settings.SuggestionEnabled
            // suggestions_size=0 では候補窓に 1 件も出ない。予測が存在しても Suggestion 状態に
            // しない(さもないと Down/Shift Enter 等が空の候補窓のまま先頭の隠れ予測を確定する)。
            || _settings.SuggestionSize <= 0
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
                includeHistory: IncludeHistorySuggest,
                includeDictionary: IncludeDictionarySuggest)
                .Count > 0;
        }
        return _suggestionCacheValue;
    }

    // keymap 照合用の状態名。サジェスト表示中は "Suggestion"(CommitFirstSuggestion 等の
    // Suggestion 固有バインドを到達可能にする。未該当キーは Composition 行へフォールバック)。
    // activatedOverride: TEST_SEND_KEY 等で「セッション状態を変えずに」クライアント宣言の
    // activated を反映して判定したいときに渡す(null なら現在の _activated)。SendKey は事前に
    // _activated へ同期するため null で呼ぶ(両経路で判定の source を一致させる)。
    private string Status(bool? activatedOverride = null)
    {
        bool act = activatedOverride ?? _activated;
        if (!act)
        {
            return "DirectInput"; // IME off。IMEOn 以外は素通し。
        }
        // 間接 IME-ON(DIRECT 状態でクライアントが activated=true を宣言): C++ session.cc は
        // converter の残状態に依らず無条件に PRECOMPOSITION を返す。これに合わせる。
        if (activatedOverride == true && !_activated)
        {
            return "Precomposition";
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
        // クライアントが IME 有効状態を宣言していれば、ディスパッチ前にセッションへ同期する
        // (commands.proto KeyEvent.activated。間接 IME off の印字キーを素通しさせる)。
        if (key.Activated.HasValue)
        {
            // 間接 IME OFF への遷移は状態遷移として扱う。入力中/変換中の preedit があれば確定して
            // から無効化する(C++ IMEOff と同じ)。確定せず _activated だけ false にすると、打鍵が
            // _typed に隠れたまま残り、後続の ON で stale テキストが復活/変換されてしまう。
            if (!key.Activated.Value && _activated
                && (_converter.CurrentState == SessionConverter.State.Conversion || _typed.Count > 0))
            {
                string flushed = _converter.Commit();
                SnapshotAndClearTyped();
                _activated = false;
                return new SessionResult { Committed = flushed, Preedit = "", Consumed = true };
            }
            _activated = key.Activated.Value;
        }
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
                // サジェスト表示中: 表示中の候補数の範囲内の shortcut だけ確定する。
                // suggestions_size が shortcut 数より小さいとき、画面に無い候補(隠れた
                // 履歴/辞書予測)を誤って確定しないよう、表示件数で idx を制限する。
                if (idx < GetSuggestions().Count && HasActiveSuggestion())
                {
                    string? sug = _converter.CommitSuggestion(idx, includeHistory: IncludeHistorySuggest, includeDictionary: IncludeDictionarySuggest);
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
        // クライアント宣言の activated を「状態を変えずに」反映して状態名を求める(SendKey は
        // 事前に _activated へ同期するが、TestSendKey はセッションを変えないため override で渡す)。
        // これで Space 等の IME-off 宣言キーが DirectInput 行で未バインド→非消費となり、
        // SendKey 側の素通しガード(!_activated)と判定が一致する(IME-off キーの横取り防止)。
        string status = Status(key.Activated);
        // 直接入力(IME off)中は印字キーを消費しない。第2項の active も key.Activated 起点に統一。
        bool active = key.Activated ?? _activated;
        bool consumed = _keyMap.GetCommand(status, key) != null
            || (active && key.Special == null && key.KeyCode is int code && code >= 0x20
                && !key.Modifiers.Contains(ModifierKey.Ctrl)
                && !key.Modifiers.Contains(ModifierKey.Alt));
        return new SessionResult { Preedit = GetPreedit(), Consumed = consumed };
    }

    public SessionResult TestSendKey(string keyString)
        => KeyParser.TryParse(keyString, out KeyEvent ke)
            ? TestSendKey(ke)
            : new SessionResult { Preedit = GetPreedit(), Consumed = false };

    // 生テキスト(かな/ソフトキーボード/TEXT_INPUT)挿入の消費可否のみ判定する(状態は変えない)。
    // IME 有効かつ非空テキストなら消費する(InsertText の消費条件と一致)。
    // activatedOverride: TEST_SEND_KEY で「状態を変えず」クライアント宣言の activated を反映して
    // 判定する(SEND_KEY 側は SyncIndirectImeOnOff で事前同期するので両経路の source を揃える)。
    public SessionResult TestInsertText(string text, bool? activatedOverride = null)
        => new SessionResult
        {
            Preedit = GetPreedit(),
            Consumed = (activatedOverride ?? _activated) && !string.IsNullOrEmpty(text),
        };

    // 間接 IME-ON/OFF の同期: クライアントが KeyEvent.activated を宣言していれば反映する。
    // 生テキスト経路(InsertText/InsertTextDirect)は keymap を通さず _activated 同期の機会が
    // 無いため、SEND_KEY 評価の直前にこれを呼んで SendKey(KeyEvent) と判定 source を揃える。
    public void SyncIndirectImeOnOff(bool? activated)
    {
        if (activated.HasValue)
        {
            _activated = activated.Value;
        }
    }

    // SEND_COMMAND: 候補の明示選択・確定・取消。
    public SessionResult SendCommand(SessionCommandType type, int id)
    {
        switch (type)
        {
            // HIGHLIGHT_CANDIDATE は注目だけ移して候補窓を閉じない(ドラッグ中などのフォーカス移動)。
            case SessionCommandType.HighlightCandidate:
                _converter.SelectCandidate(id);
                return Current(true);
            // SELECT_CANDIDATE は候補窓を閉じる=選択候補を確定する(commands.proto: SELECT は
            // ウィンドウを閉じ、HIGHLIGHT は閉じない)。マウス/タッチのクリック選択がこの経路。
            // SUBMIT_CANDIDATE と同じ確定処理へ合流する(以前は HIGHLIGHT と同じく選択だけして
            // 確定しなかったため、クリックしても preedit に残り何も commit されなかった)。
            case SessionCommandType.SelectCandidate:
            case SessionCommandType.SubmitCandidate:
                // 入力中(サジェスト)はサジェスト候補を直接確定する。
                if (_converter.CurrentState == SessionConverter.State.Composition)
                {
                    string? sug = _converter.CommitSuggestion(id, includeHistory: IncludeHistorySuggest, includeDictionary: IncludeDictionarySuggest);
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
                // 変換中の候補確定は注目文節までを確定し、後続文節は変換状態のまま残す
                // (C++ 同様の部分確定)。残り文節があれば preedit を保持して消費継続する。
                {
                    string head = _converter.CommitHeadToFocusedSegments();
                    if (_converter.CurrentState == SessionConverter.State.Conversion)
                    {
                        // 部分確定: 確定テキストを返しつつ残り変換を継続する。_typed/_lastCommitted を
                        // 残り読み・確定済み読みへ再同期し、後続の Backspace/Undo が古い全入力から
                        // 既確定の先頭テキストを復活させないようにする。
                        ResyncAfterPartialCommit();
                        return new SessionResult
                        {
                            Committed = head,
                            Preedit = _converter.GetPreedit(),
                            Consumed = true,
                        };
                    }
                    // 全体確定(単文節/最終文節)。
                    SnapshotAndClearTyped();
                    return new SessionResult { Committed = head, Preedit = "", Consumed = true };
                }
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
            case SessionCommandType.Undo:
                // ツールバー/クライアント駆動の取り消しもキーボード Undo と同じ復元処理へ回す。
                return Undo();
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
        // 各 rune が確定(候補コミット等)を生むことがあるため、Committed を取りこぼさず累積する。
        string committed = string.Empty;
        foreach (global::System.Text.Rune rune in text.EnumerateRunes())
        {
            last = InsertChar(rune.ToString());
            committed += last.Committed;
        }
        return new SessionResult
        {
            Committed = committed,
            Preedit = last.Preedit,
            Consumed = last.Consumed,
            Command = last.Command,
        };
    }

    // input_style=AS_IS の key_string 挿入。ローマ字表変換をかけず literal を保持する
    // (AS_IS クライアントが意図的に Latin テキストやカスタムローマ字規則に当たる文字を
    // 送ったとき、変換/pending 化させずそのまま composer へ入れる)。
    public SessionResult InsertTextAsIs(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
        string committed = string.Empty;
        SessionResult last = Current(true);
        foreach (global::System.Text.Rune rune in text.EnumerateRunes())
        {
            last = InsertCharAsIs(rune.ToString());
            committed += last.Committed;
        }
        return new SessionResult
        {
            Committed = committed,
            Preedit = last.Preedit,
            Consumed = last.Consumed,
        };
    }

    private SessionResult InsertCharAsIs(string ch)
    {
        string committed = string.Empty;
        if (_converter.CurrentState == SessionConverter.State.Conversion)
        {
            committed = _converter.Commit();
            SnapshotAndClearTyped();
        }
        _converter.InsertCharacterAsIs(ch);
        _typed.Add(new TypedKey(ch, true)); // AS_IS: 再構築時もローマ字変換しない。
        return new SessionResult { Committed = committed, Preedit = GetPreedit(), Consumed = true };
    }

    // input_style=DIRECT_INPUT の key_string 処理(C++ session.cc:1486-1544 相当)。
    // precomposition(入力前)では key_string を即時確定する(ソフトキーボード/直接テキスト)。
    // ただし半角 ASCII 1 文字(key_code==key_string)は echo back(未消費・確定なし)。
    // 変換中は変換を確定してから直接テキストを確定する。preedit(入力中)では AS_IS と同じ扱い。
    // keyCode は半角 ASCII echo back 判定に使う(key_code==key_string の単一コードポイント)。
    public SessionResult InsertTextDirect(string text, int? keyCode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
        // precomposition proxy: 入力も変換中も無い状態。
        if (_converter.CurrentState == SessionConverter.State.Composition && _typed.Count == 0)
        {
            // 半角 ASCII 1 文字(key_code==key_string[0] かつ空白以外)は echo back(未消費)。
            // C++ EchoBack 相当: converter をリセットし、キーをアプリへ委ねる。
            if (IsHalfWidthAsciiEcho(text, keyCode))
            {
                _converter.Reset();
                return new SessionResult { Preedit = "", Consumed = false };
            }
            // それ以外の直接テキストは即時確定する(Result として出力)。
            return new SessionResult { Committed = text, Preedit = "", Consumed = true };
        }
        // 変換中: まず変換を確定し、続けて直接テキストを確定する(C++ should_commit + DIRECT_INPUT)。
        if (_converter.CurrentState == SessionConverter.State.Conversion)
        {
            string committed = _converter.Commit();
            SnapshotAndClearTyped();
            return new SessionResult { Committed = committed + text, Preedit = "", Consumed = true };
        }
        // 入力中(preedit): C++ 仕様で DIRECT_INPUT は AS_IS と同じ扱い。ローマ字表変換を止めて
        // literal をそのまま合成する(InsertTextAsIs。'n' やカスタムローマ字規則に当たる文字を
        // 変換/pending 化させない)。
        return InsertTextAsIs(text);
    }

    // TEST_SEND_KEY の DIRECT_INPUT 判定(セッション状態は変えない)。C++ session.cc:459-466 相当:
    // precomposition の DIRECT_INPUT(INSERT_CHARACTER)は echo back 扱いで未消費を返す。
    public SessionResult TestInsertTextDirect(string text, int? keyCode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SessionResult { Preedit = GetPreedit(), Consumed = false };
        }
        if (_converter.CurrentState == SessionConverter.State.Composition && _typed.Count == 0)
        {
            // precomposition: 半角 ASCII 1 文字(echo back)だけ未消費。それ以外の直接テキストは
            // 実 send パス(InsertTextDirect)が確定して消費するため、test 側も消費を返す。
            // 全 DIRECT_INPUT を未消費としていると test-before-send クライアントが非 echo-back の
            // 文字(例 'あ')をアプリへ素通しさせ、サーバ確定と二重入力/取りこぼしになる。
            return IsHalfWidthAsciiEcho(text, keyCode)
                ? new SessionResult { Preedit = "", Consumed = false }
                : new SessionResult { Preedit = "", Consumed = true };
        }
        return TestInsertText(text);
    }

    // key_string が単一コードポイントの半角 ASCII 印字(0x21-0x7E)で key_code と一致するか。
    private static bool IsHalfWidthAsciiEcho(string text, int? keyCode)
    {
        if (keyCode is not int kc || kc < 0x21 || kc > 0x7E)
        {
            return false; // 非 ASCII / 制御文字 / 半角空白(0x20)は echo back 対象外。
        }
        global::System.Text.Rune? only = null;
        foreach (global::System.Text.Rune rune in text.EnumerateRunes())
        {
            if (only != null)
            {
                return false; // 複数コードポイントは対象外。
            }
            only = rune;
        }
        return only is global::System.Text.Rune r && r.Value == kc;
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
                // 先頭文節のみ確定し、残り文節は変換状態のまま編集を継続する(C++ 同名コマンド相当)。
                // 単一文節・最終文節では CommitFirstSegment が全体確定にフォールバックする。
                if (_converter.CurrentState == SessionConverter.State.Conversion)
                {
                    string committed = _converter.CommitFirstSegment();
                    if (_converter.CurrentState == SessionConverter.State.Conversion)
                    {
                        // 部分確定: 残りを変換継続(_typed/_lastCommitted を再同期)。
                        ResyncAfterPartialCommit();
                        return new SessionResult
                        {
                            Committed = committed,
                            Preedit = _converter.GetPreedit(),
                            Consumed = true,
                        };
                    }
                    SnapshotAndClearTyped();
                    return new SessionResult { Committed = committed, Preedit = "", Consumed = true };
                }
                return ConsumeNoOpWhile(_typed.Count > 0);
            case "Convert":
            // ConvertWithoutHistory(F2): 履歴を使わない変換。no-history 変換は未実装のため
            // 通常変換へエイリアスして消費する(プリセットが composition 中の F2 に割当てており、
            // 未対応だと F2 がアプリへ漏れる)。
            case "ConvertWithoutHistory":
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
                _converter.SegmentFocusRight();
                return Current(true);
            case "SegmentFocusLeft":
                _converter.SegmentFocusLeft();
                return Current(true);
            // End/Ctrl+Right 等は最終文節へ、Home/Ctrl+Left 等は先頭文節へ「一気に」移動する。
            // 1 段ずつの右/左ハンドラを共有していると、3 文節以上で End/Home が隣の文節までしか
            // 動かず、その後の選択・確定が意図と違う文節に作用する(C++ SegmentFocusLast/First 相当)。
            case "SegmentFocusLast":
                _converter.SegmentFocusLast();
                return Current(true);
            case "SegmentFocusFirst":
                _converter.SegmentFocusFirst();
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
            case "Revert":
                // C++ Session::Revert 相当。composition/conversion は precomposition へ
                // キャンセルして消費。precomposition では学習を戻し(本実装は履歴 revert 未対応)
                // キーをエコーバックする(消費せずホストへ素通し = C++ EchoBack)。
                if (_converter.CurrentState == SessionConverter.State.Conversion
                    || _typed.Count > 0)
                {
                    _converter.Reset();
                    _typed.Clear();
                    return Current(true);
                }
                return new SessionResult { Preedit = string.Empty, Consumed = false };
            case "Undo":
                return Undo();
            case "CommitFirstSuggestion":
            {
                // サジェスト表示中に先頭候補を確定する(Shift Enter / Ctrl Enter)。
                string? sug = _converter.CommitSuggestion(0, includeHistory: IncludeHistorySuggest, includeDictionary: IncludeDictionarySuggest);
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
            // KOTOERI は半角化(F8/Ctrl ;/Option a)を DisplayAsHalfWidth 名で割当てる(同義)。
            case "DisplayAsHalfWidth":
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
                            0, includeHistory: IncludeHistorySuggest, includeDictionary: IncludeDictionarySuggest);
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
            // 入力モード切替。入力モードの永続状態機械は未実装だが、これらは IME 用キー
            // (かな/カナ/英数キー等)に割り当たるため、ホストへ素のキーを漏らさないよう必ず消費する。
            case "CompositionModeHiragana":
                // 現在の composition をひらがな表記へ寄せて消費(かなモードへ切替相当)。
                _converter.ConvertToTransliteration(c => c.GetHiragana());
                return Current(true);
            case "CompositionModeFullKatakana":
                _converter.ConvertToTransliteration(c => c.GetFullKatakana());
                return Current(true);
            case "ToggleAlphanumericMode":
            case "SwitchKanaType":
            case "CompositionModeSwitchKanaType":
                // モード状態機械が無いため preedit を保ったまま消費のみ(キー透過を防ぐ)。
                return new SessionResult { Preedit = GetPreedit(), Consumed = true };
            case "Reconvert":
                // 再変換(DirectInput/Precomposition の Henkan 等にバインド)。C++
                // RequestConvertReverse(session.cc) は PRECOMPOSITION/DIRECT で無条件に
                // consumed=true にし CONVERT_REVERSE callback を返す。逆変換 API(周辺文書の
                // 読み取得→再変換)は未移植のため、ここでは少なくともキーを消費してホストへ
                // 素の Henkan を漏らさない(到達時点で必ず Precomposition/DirectInput=消費が忠実)。
                // TODO(C7): CONVERT_REVERSE callback 配管 + GetReadingText 相当の逆変換を移植。
                return new SessionResult { Preedit = GetPreedit(), Consumed = true };
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
        _typed.Add(new TypedKey(ch, false)); // 通常: ローマ字変換あり。
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
        // 各キーを元のモード(romaji/AS_IS)で再生する。AS_IS の literal を romaji 変換し直して
        // 壊さない(例: literal "ka" を一部削除しても "か" にならない)。
        ReplayTyped(_typed);
        return Current(true);
    }

    private SessionResult Current(bool consumed)
        => new() { Preedit = GetPreedit(), Consumed = consumed };

    // 未実装の編集コマンド(カーソル移動/文節幅)の共通処理。active(入力中/変換中)なら
    // no-op として消費し、そうでなければアプリへ素通しする。
    private SessionResult ConsumeNoOpWhile(bool active)
        => active ? Current(true) : new SessionResult { Preedit = GetPreedit(), Consumed = false };
}
