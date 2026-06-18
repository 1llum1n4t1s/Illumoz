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

    public EngineServer(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null, string? dataDir = null)
    {
        _engine = engine;
        _handler = new SessionHandler(engine, keyMap, rewriter);
        _dataDir = dataDir;
        ApplyConfig();
    }

    public SessionHandler Handler => _handler;
    public ConfigManager Config => _config;

    // config.character_form_rules から構築した文字形マネージャ(preedit / conversion)。
    // ApplyConfig で更新される。既定は C++ Preedit 既定ルール。
    public Mozc.Base.CharacterFormManager PreeditFormManager { get; private set; }
        = Mozc.Base.CharacterFormManager.CreatePreeditDefault();
    public Mozc.Base.CharacterFormManager ConversionFormManager { get; private set; }
        = Mozc.Base.CharacterFormManager.CreatePreeditDefault();

    // Config を session の挙動へ反映する(履歴学習レベル + keymap プリセット)。
    public void ApplyConfig()
    {
        Mozc.Config.Config c = _config.GetConfig();
        bool learn = c.HistoryLearningLevel == Mozc.Config.Config.Types.HistoryLearningLevel.DefaultHistory;
        _handler.History.LearningEnabled = learn;

        // CommandRewriter があれば config のモードフラグを反映する(C++ command_rewriter は
        // config.incognito_mode / presentation_mode / use_*_suggest を直接参照する)。
        ApplyConfigToCommandRewriter(c);

        // 文字形ルールを反映する。rules が無ければ既定(Preedit 既定)を維持。
        BuildCharacterFormManagers(c);

        // SessionKeymap に対応するプリセットが src/data にあれば差し替える。
        if (_dataDir != null)
        {
            string name = KeymapName(c.SessionKeymap);
            KeyMap? km = KeymapPresets.Load(_dataDir, name);
            if (km != null)
            {
                _handler.SetKeyMap(km);
            }
        }

        // カスタムローマ字表(bytes=TSV)が設定されていれば composer に反映する。
        if (c.CustomRomanTable.Length > 0)
        {
            _engine.SetRomanTable(c.CustomRomanTable.ToStringUtf8());
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
                break;
            case Mozc.Config.Config.Types.FundamentalCharacterForm.FundamentalHalfWidth:
                _engine.AddRomanRule(" ", " ");
                break;
        }
    }

    // config.character_form_rules から preedit / conversion の文字形マネージャを構築。
    private void BuildCharacterFormManagers(Mozc.Config.Config c)
    {
        if (c.CharacterFormRules.Count == 0)
        {
            return; // 既定を維持。
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
    }

    // config の CharacterForm → Base の CharacterForm。LAST_FORM は未実装のため
    // 既定(FullWidth)へ解決する(履歴記憶は将来対応)。
    private static Mozc.Base.CharacterForm MapForm(Mozc.Config.Config.Types.CharacterForm f) => f switch
    {
        Mozc.Config.Config.Types.CharacterForm.HalfWidth => Mozc.Base.CharacterForm.HalfWidth,
        Mozc.Config.Config.Types.CharacterForm.FullWidth => Mozc.Base.CharacterForm.FullWidth,
        _ => Mozc.Base.CharacterForm.FullWidth, // LAST_FORM
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
        catch (global::System.Exception)
        {
            return CommandCodec.EncodeOutput(new Output { ErrorOccured = true });
        }
        Output output = EvalWithConfig(input);
        return CommandCodec.EncodeOutput(output);
    }

    // Config 系コマンドは EngineServer 層(ConfigManager 所有)で処理し、
    // それ以外は SessionHandler に委譲する。
    private Output EvalWithConfig(Input input)
    {
        switch (input.Type)
        {
            case CommandType.GetConfig:
                return new Output { Consumed = true, ConfigBytes = _config.Serialize() };
            case CommandType.SetConfig:
                try
                {
                    SetConfig(Mozc.Config.Config.Parser.ParseFrom(input.ConfigBytes));
                    return new Output { Consumed = true, ConfigBytes = _config.Serialize() };
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {
                    return new Output { ErrorOccured = true };
                }
            default:
                return _handler.EvalCommand(input);
        }
    }

    // C++ ワイヤー互換(protobuf)経路。commands.proto の Input/Output を直接やり取りする。
    public byte[] HandleProtoRequest(byte[] request)
    {
        Input input;
        try
        {
            input = ProtoBridge.DecodeInput(request);
        }
        catch (global::System.Exception)
        {
            return ProtoBridge.EncodeOutput(new Output { ErrorOccured = true });
        }
        Output output = EvalWithConfig(input);
        return ProtoBridge.EncodeOutput(output, ShortcutChars(_config.GetConfig().SelectionShortcut));
    }

    // SelectionShortcut → 候補ショートカット文字列。
    private static string ShortcutChars(Mozc.Config.Config.Types.SelectionShortcut s) => s switch
    {
        Mozc.Config.Config.Types.SelectionShortcut.Shortcut123456789 => "123456789",
        Mozc.Config.Config.Types.SelectionShortcut.ShortcutAsdfghjkl => "asdfghjkl",
        _ => string.Empty, // NO_SHORTCUT
    };
}
