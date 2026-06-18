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

    // Config を session の挙動へ反映する(履歴学習レベル + keymap プリセット)。
    public void ApplyConfig()
    {
        Mozc.Config.Config c = _config.GetConfig();
        bool learn = c.HistoryLearningLevel == Mozc.Config.Config.Types.HistoryLearningLevel.DefaultHistory;
        _handler.History.LearningEnabled = learn;

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
        return ProtoBridge.EncodeOutput(output);
    }
}
