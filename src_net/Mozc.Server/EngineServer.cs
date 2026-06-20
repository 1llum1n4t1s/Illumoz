using Mozc.Engine;
using Mozc.Rewriter;
using Mozc.Session;

namespace Mozc.Server;

// C++ mozc_server の中核: SessionHandler を保持し、IPC で来た byte[] リクエストを
// Input にデコード → 評価 → Output を byte[] にエンコードして返す。
// 実トランスポート(名前付きパイプ/Unix socket サーバ)は Mozc.Ipc の server 実装と接続する。
public sealed class EngineServer
{
    private readonly MozcEngine _engine;
    private readonly SessionHandler _handler;
    private readonly ConfigManager _config = new();
    // keymap プリセットを config から再ロードするための src/data ルート(任意)。
    private readonly string? _dataDir;
    // 構築時のキーマップ。custom/preset が無い config に戻ったとき復元する基準。
    private readonly KeyMap _initialKeyMap;

    public EngineServer(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null, string? dataDir = null)
    {
        _engine = engine;
        _handler = new SessionHandler(engine, keyMap, rewriter);
        _dataDir = dataDir;
        _initialKeyMap = keyMap;
        ApplyConfig();
    }

    public SessionHandler Handler => _handler;
    public ConfigManager Config => _config;

    // config.character_form_rules から構築した文字形マネージャ(preedit / conversion)。
    // ApplyConfig で更新される。既定は C++ Preedit 既定ルール。
    public Mozc.Base.CharacterFormManager PreeditFormManager { get; private set; }
        = Mozc.Base.CharacterFormManager.CreatePreeditDefault();
    public Mozc.Base.CharacterFormManager ConversionFormManager { get; private set; }
        = Mozc.Base.CharacterFormManager.CreateConversionDefault();

    // Config を session の挙動へ反映する(履歴学習レベル + keymap プリセット)。
    public void ApplyConfig()
    {
        Mozc.Config.Config c = _config.GetConfig();
        // シークレットモードでは履歴学習を止める(プライバシー)。学習レベルとの AND。
        bool learn = c.HistoryLearningLevel == Mozc.Config.Config.Types.HistoryLearningLevel.DefaultHistory
            && !c.IncognitoMode;
        _handler.History.LearningEnabled = learn;

        // セッション共有の挙動設定(サジェスト可否/件数/シークレット)を反映する。
        // presentation_mode はサジェストを一時停止する(use_*_suggest が有効でも抑止)。
        _handler.Settings.SuggestionEnabled = !c.PresentationMode
            && (c.UseHistorySuggest || c.UseDictionarySuggest || c.UseRealtimeConversion);
        // 個別のサジェストソース可否も保持し、Session 側で履歴/辞書を選択的に出す。
        _handler.Settings.UseHistorySuggest = c.UseHistorySuggest;
        _handler.Settings.UseDictionarySuggest = c.UseDictionarySuggest;
        _handler.Settings.UseRealtimeConversion = c.UseRealtimeConversion;
        _handler.Settings.SuggestionSize = (int)c.SuggestionsSize;
        _handler.Settings.IncognitoMode = c.IncognitoMode;
        _handler.Settings.SelectionShortcuts = ShortcutChars(c.SelectionShortcut);

        // CommandRewriter があれば config のモードフラグを反映する(C++ command_rewriter は
        // config.incognito_mode / presentation_mode / use_*_suggest を直接参照する)。
        ApplyConfigToCommandRewriter(c);

        // 文字形ルールを反映する。rules が無ければ既定(Preedit 既定)を維持。
        BuildCharacterFormManagers(c);

        // キーマップを反映する。custom_keymap_table(または session_keymap=CUSTOM)が
        // あれば最優先で適用し、無ければ session_keymap のプリセットを src/data から読む。
        if (c.CustomKeymapTable.Length > 0)
        {
            var km = new KeyMap();
            km.LoadFromString(c.CustomKeymapTable.ToStringUtf8());
            // 不正/空のカスタム表なら構築時キーマップへ戻す(直前のカスタムが残らないように)。
            _handler.SetKeyMap(km.EntryCount > 0 ? km : _initialKeyMap);
        }
        else if (_dataDir != null
            && c.SessionKeymap != Mozc.Config.Config.Types.SessionKeymap.Custom
            && c.SessionKeymap != Mozc.Config.Config.Types.SessionKeymap.None)
        {
            string name = KeymapName(c.SessionKeymap);
            KeyMap? km = KeymapPresets.Load(_dataDir, name);
            // プリセットが解決できなければ構築時キーマップへフォールバック。
            _handler.SetKeyMap(km ?? _initialKeyMap);
        }
        else
        {
            // custom 表もプリセットも無い config → 構築時キーマップを復元する。
            // (per-request の一時カスタムキーマップが後続セッションへ残るのを防ぐ)
            _handler.SetKeyMap(_initialKeyMap);
        }

        // カスタムローマ字表(bytes=TSV)が設定されていれば composer に反映する。
        // 空になった(カスタム解除)なら既定のローマ字表へ戻す。
        if (c.CustomRomanTable.Length > 0)
        {
            _engine.SetRomanTable(c.CustomRomanTable.ToStringUtf8());
        }
        else
        {
            _engine.ResetRomanTable();
        }

        // 句読点方式を composer のローマ字ルールへ反映する(C++ punctuation_method)。
        (string comma, string period) = PunctuationFor(c.PunctuationMethod);
        _engine.AddRomanRule(",", comma);
        _engine.AddRomanRule(".", period);

        // 記号方式(括弧/中黒・スラッシュ)を反映する(C++ symbol_method)。
        (string open, string close, string slash) = SymbolFor(c.SymbolMethod);
        _engine.AddRomanRule("[", open);
        _engine.AddRomanRule("]", close);
        _engine.AddRomanRule("/", slash);

        // スペースの字形(全角/半角)を反映する(C++ space_character_form)。
        // INPUT_MODE はモード追従のため上書きしない。
        switch (c.SpaceCharacterForm)
        {
            case Mozc.Config.Config.Types.FundamentalCharacterForm.FundamentalFullWidth:
                _engine.AddRomanRule(" ", "　");
                _handler.Settings.SpaceForm = SpaceForm.Full;
                break;
            case Mozc.Config.Config.Types.FundamentalCharacterForm.FundamentalHalfWidth:
                _engine.AddRomanRule(" ", " ");
                _handler.Settings.SpaceForm = SpaceForm.Half;
                break;
            default:
                // FUNDAMENTAL_INPUT_MODE 等(モード追従)。前回の全角/半角キャッシュが残ると
                // InsertSpace が古い字形のままになるため、既定(半角)へ戻す。
                _handler.Settings.SpaceForm = SpaceForm.Half;
                break;
        }

        // ローマ字表/ルールが変わったので、idle な既存セッションの composer を作り直して
        // 設定変更を次の入力から反映する(セッション使い回しでも設定が効くように)。
        _handler.RefreshIdleComposers();
    }

    // config.character_form_rules から preedit / conversion の文字形マネージャを構築。
    private void BuildCharacterFormManagers(Mozc.Config.Config c)
    {
        if (c.CharacterFormRules.Count == 0)
        {
            // ルールが空(初期化 or 後から解除)なら既定マネージャへ戻す。前の config の
            // 幅設定が残り続ける不具合を防ぐため、解除時も必ず再構築・伝播する。
            PreeditFormManager = Mozc.Base.CharacterFormManager.CreatePreeditDefault();
            // 変換側は preedit と別の既定(ASCII/数字/記号は LAST_FORM)。preedit 既定を流用すると
            // abc/123 等が常に全角へ書き換わり、半角候補を選んでも記憶されない。
            ConversionFormManager = Mozc.Base.CharacterFormManager.CreateConversionDefault();
            PropagateConversionFormManager();
            return;
        }
        var preedit = new global::System.Collections.Generic.List<(string, Mozc.Base.CharacterForm)>();
        var conversion = new global::System.Collections.Generic.List<(string, Mozc.Base.CharacterForm)>();
        foreach (Mozc.Config.Config.Types.CharacterFormRule rule in c.CharacterFormRules)
        {
            preedit.Add((rule.Group, MapForm(rule.PreeditCharacterForm)));
            conversion.Add((rule.Group, MapForm(rule.ConversionCharacterForm)));
        }
        PreeditFormManager = Mozc.Base.CharacterFormManager.FromRules(preedit);
        ConversionFormManager = Mozc.Base.CharacterFormManager.FromRules(conversion);
        PropagateConversionFormManager();
    }

    // pipeline 内の CharacterFormRewriter に conversion 用マネージャを反映する。
    private void PropagateConversionFormManager()
    {
        if (_handler.Rewriter is RewriterMerger merger)
        {
            foreach (IRewriter r in merger.Rewriters)
            {
                if (r is CharacterFormRewriter cfr)
                {
                    cfr.SetManager(ConversionFormManager);
                }
            }
        }
    }

    // config の CharacterForm → Base の CharacterForm(LAST_FORM / NO_CONVERSION も対応)。
    private static Mozc.Base.CharacterForm MapForm(Mozc.Config.Config.Types.CharacterForm f) => f switch
    {
        Mozc.Config.Config.Types.CharacterForm.HalfWidth => Mozc.Base.CharacterForm.HalfWidth,
        Mozc.Config.Config.Types.CharacterForm.FullWidth => Mozc.Base.CharacterForm.FullWidth,
        Mozc.Config.Config.Types.CharacterForm.LastForm => Mozc.Base.CharacterForm.LastForm,
        // NO_CONVERSION は「変換しない」。既定の FullWidth に潰すと ASCII/数字/カナを
        // そのままにしたいユーザー設定を壊すため明示的に NoConversion を返す。
        Mozc.Config.Config.Types.CharacterForm.NoConversion => Mozc.Base.CharacterForm.NoConversion,
        _ => Mozc.Base.CharacterForm.FullWidth,
    };

    // CommandRewriter にモード状態を流し込む(pipeline 内を探索)。
    private void ApplyConfigToCommandRewriter(Mozc.Config.Config c)
    {
        if (_handler.Rewriter is not RewriterMerger merger)
        {
            return;
        }
        foreach (IRewriter r in merger.Rewriters)
        {
            if (r is CommandRewriter cmd)
            {
                cmd.IncognitoMode = c.IncognitoMode;
                cmd.PresentationMode = c.PresentationMode;
                cmd.SuggestionEnabled =
                    c.UseHistorySuggest || c.UseDictionarySuggest || c.UseRealtimeConversion;
            }
        }
    }

    // SymbolMethod → (開き括弧, 閉じ括弧, スラッシュ/中黒)。
    private static (string, string, string) SymbolFor(Mozc.Config.Config.Types.SymbolMethod m) => m switch
    {
        Mozc.Config.Config.Types.SymbolMethod.SquareBracketSlash => ("［", "］", "／"),
        Mozc.Config.Config.Types.SymbolMethod.CornerBracketSlash => ("「", "」", "／"),
        Mozc.Config.Config.Types.SymbolMethod.SquareBracketMiddleDot => ("［", "］", "・"),
        _ => ("「", "」", "・"), // CORNER_BRACKET_MIDDLE_DOT(既定)
    };

    // PunctuationMethod → (読点相当, 句点相当)。
    private static (string, string) PunctuationFor(Mozc.Config.Config.Types.PunctuationMethod m) => m switch
    {
        Mozc.Config.Config.Types.PunctuationMethod.CommaPeriod => ("，", "．"),
        Mozc.Config.Config.Types.PunctuationMethod.ToutenPeriod => ("、", "．"),
        Mozc.Config.Config.Types.PunctuationMethod.CommaKuten => ("，", "。"),
        _ => ("、", "。"), // TOUTEN_KUTEN(既定)
    };

    // protobuf enum → C++ OriginalName 文字列(KeymapPresets が解決する)。
    private static string KeymapName(Mozc.Config.Config.Types.SessionKeymap k) => k switch
    {
        Mozc.Config.Config.Types.SessionKeymap.Msime => "MSIME",
        Mozc.Config.Config.Types.SessionKeymap.Atok => "ATOK",
        Mozc.Config.Config.Types.SessionKeymap.Kotoeri => "KOTOERI",
        Mozc.Config.Config.Types.SessionKeymap.Mobile => "MOBILE",
        Mozc.Config.Config.Types.SessionKeymap.Chromeos => "CHROMEOS",
        _ => "CUSTOM",
    };

    // 設定を更新して即時反映する(SetConfig 経路)。
    public void SetConfig(Mozc.Config.Config config)
    {
        _config.SetConfig(config);
        ApplyConfig();
    }

    // IPC リクエスト処理本体(transport から呼ばれる)。
    public byte[] HandleRequest(byte[] request)
    {
        Input input;
        try
        {
            input = CommandCodec.DecodeInput(request);
        }
        catch (global::System.Exception ex)
        {
            Mozc.Base.MozcLog.Error("HandleRequest decode", ex);
            return CommandCodec.EncodeOutput(new Output { ErrorOccured = true });
        }
        // EvalCommand 以降の想定外例外も必ずエラー Output に変換して返す
        // (応答を返さないと IPC 層で握りつぶされクライアントが応答を得られない)。
        try
        {
            Output output = EvalWithConfig(input).Output;
            return CommandCodec.EncodeOutput(output);
        }
        catch (global::System.Exception ex)
        {
            Mozc.Base.MozcLog.Error("HandleRequest eval", ex);
            return CommandCodec.EncodeOutput(new Output { ErrorOccured = true });
        }
    }

    // EvalWithConfig の結果。Output に加え、エンコード時に使う「実効 config 依存の値」
    // (候補ショートカット文字列 / preedit 形変換済み文字列)を request config が有効な
    // うちに捕捉して持ち回る。per-request config を finally で戻した後にこれらを再計算すると
    // 設定が反映されないため(selection_shortcut / preedit_character_form)。
    private readonly record struct EvalResult(Output Output, string Shortcuts, string? PreeditOverride);

    // 現在の実効 config から、エンコードに必要な shortcut/preedit を捕捉する。
    private EvalResult Capture(Output output)
    {
        string shortcuts = ShortcutChars(_config.GetConfig().SelectionShortcut);
        string? preedit = output.Preedit.Length != 0
            ? PreeditFormManager.ConvertString(output.Preedit)
            : null;
        return new EvalResult(output, shortcuts, preedit);
    }

    // Config 系コマンドは EngineServer 層(ConfigManager 所有)で処理し、
    // それ以外は SessionHandler に委譲する。
    private EvalResult EvalWithConfig(Input input)
    {
        switch (input.Type)
        {
            case CommandType.GetConfig:
                return Capture(new Output { Consumed = true, ConfigBytes = _config.Serialize() });
            case CommandType.SetConfig:
                try
                {
                    SetConfig(Mozc.Config.Config.Parser.ParseFrom(input.ConfigBytes));
                    return Capture(new Output { Consumed = true, ConfigBytes = _config.Serialize() });
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {
                    return Capture(new Output { ErrorOccured = true });
                }
            default:
            {
                // クライアントがリクエストに config を添付(MozcSessionClient.Preferences 経由の
                // per-request 設定)していれば、このリクエストの間だけ適用する。共有 ConfigManager を
                // 恒久的に書き換えると他クライアントへ漏れたり shutdown 時に保存されるため、
                // 評価後に元の config へ必ず戻す。
                byte[]? savedConfig = null;
                if (input.ConfigBytes.Length != 0)
                {
                    try
                    {
                        savedConfig = _config.Serialize();
                        SetConfig(Mozc.Config.Config.Parser.ParseFrom(input.ConfigBytes));
                    }
                    catch (Google.Protobuf.InvalidProtocolBufferException)
                    {
                        savedConfig = null; // 壊れた config は無視して通常処理を続ける。
                    }
                }
                try
                {
                    Output output = _handler.EvalCommand(input);
                    ApplyConverterCommand(output.ConverterCommand);
                    // LAST_FORM 学習: 確定文字列の字形を ConversionFormManager に記憶させ、
                    // 以降の変換/保存(character_form.db)へ反映する。
                    if (output.Result.Length != 0)
                    {
                        ConversionFormManager.GuessAndSetCharacterForm(output.Result);
                    }
                    // request config(per-request の selection_shortcut / preedit_character_form)が
                    // 有効なうちに encode 用の値を捕捉する。finally の復元後では設定が反映されない。
                    return Capture(output);
                }
                finally
                {
                    if (savedConfig != null)
                    {
                        SetConfig(Mozc.Config.Config.Parser.ParseFrom(savedConfig));
                    }
                }
            }
        }
    }

    // 確定したコマンド候補(incognito/presentation トグル)を config に反映し即時適用する。
    private void ApplyConverterCommand(Mozc.Converter.Candidate.CommandType command)
    {
        if (command == Mozc.Converter.Candidate.CommandType.DefaultCommand)
        {
            return;
        }
        Mozc.Config.Config c = _config.GetConfig().Clone();
        switch (command)
        {
            case Mozc.Converter.Candidate.CommandType.EnableIncognitoMode:
                c.IncognitoMode = true;
                break;
            case Mozc.Converter.Candidate.CommandType.DisableIncognitoMode:
                c.IncognitoMode = false;
                break;
            case Mozc.Converter.Candidate.CommandType.EnablePresentationMode:
                c.PresentationMode = true;
                break;
            case Mozc.Converter.Candidate.CommandType.DisablePresentationMode:
                c.PresentationMode = false;
                break;
            default:
                return;
        }
        SetConfig(c);
    }

    // C++ ワイヤー互換(protobuf)経路。commands.proto の Input/Output を直接やり取りする。
    public byte[] HandleProtoRequest(byte[] request)
    {
        Input input;
        try
        {
            input = ProtoBridge.DecodeInput(request);
        }
        catch (global::System.Exception ex)
        {
            Mozc.Base.MozcLog.Error("HandleProtoRequest decode", ex);
            return ProtoBridge.EncodeOutput(new Output { ErrorOccured = true });
        }
        // per-request config が有効なうちに EvalWithConfig が捕捉した shortcut/preedit を使う
        // (preedit には preedit_character_form を適用済み。selection_shortcut も request 値)。
        // EvalCommand 以降の想定外例外も必ずエラー Output に変換して返す(無応答だと IPC 層で
        // 握りつぶされクライアントが応答を得られない)。
        try
        {
            EvalResult r = EvalWithConfig(input);
            return ProtoBridge.EncodeOutput(r.Output, r.Shortcuts, r.PreeditOverride);
        }
        catch (global::System.Exception ex)
        {
            Mozc.Base.MozcLog.Error("HandleProtoRequest eval", ex);
            return ProtoBridge.EncodeOutput(new Output { ErrorOccured = true });
        }
    }

    // SelectionShortcut → 候補ショートカット文字列。
    private static string ShortcutChars(Mozc.Config.Config.Types.SelectionShortcut s) => s switch
    {
        Mozc.Config.Config.Types.SelectionShortcut.Shortcut123456789 => "123456789",
        Mozc.Config.Config.Types.SelectionShortcut.ShortcutAsdfghjkl => "asdfghjkl",
        _ => string.Empty, // NO_SHORTCUT
    };
}
