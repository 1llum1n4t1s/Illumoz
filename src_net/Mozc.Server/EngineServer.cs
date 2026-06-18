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

    public EngineServer(MozcEngine engine, KeyMap keyMap, IRewriter? rewriter = null)
    {
        _handler = new SessionHandler(engine, keyMap, rewriter);
    }

    public SessionHandler Handler => _handler;

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
