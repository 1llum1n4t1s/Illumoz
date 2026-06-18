using Mozc.Engine;
using Mozc.Rewriter;
using Mozc.Session;

namespace Mozc.Server;

// C++ mozc_server の中核: SessionHandler を保持し、IPC で来た byte[] リクエストを
// Input にデコード → 評価 → Output を byte[] にエンコードして返す。
// 実トランスポート(名前付きパイプ/Unix socket サーバ)は Mozc.Ipc の server 実装と接続する。
public sealed class EngineServer
{
    private readonly SessionHandler _handler;
    private readonly ConfigManager _config = new();

    public EngineServer(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null)
    {
        _handler = new SessionHandler(engine, keyMap, rewriter);
        ApplyConfig();
    }

    public SessionHandler Handler => _handler;
    public ConfigManager Config => _config;

    // Config を session の挙動へ反映する(現状: 履歴学習レベル)。
    public void ApplyConfig()
    {
        Mozc.Config.Config c = _config.GetConfig();
        bool learn = c.HistoryLearningLevel == Mozc.Config.Config.Types.HistoryLearningLevel.DefaultHistory;
        _handler.History.LearningEnabled = learn;
    }

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
        Output output = _handler.EvalCommand(input);
        return CommandCodec.EncodeOutput(output);
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
        Output output = _handler.EvalCommand(input);
        return ProtoBridge.EncodeOutput(output);
    }
}
