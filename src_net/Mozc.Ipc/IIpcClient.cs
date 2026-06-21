namespace Mozc.Ipc;

// C++ src/ipc/ipc.h の IPCClientInterface 相当(クライアント側の最小契約)。
// ペイロードは「裸の protobuf バイト列」。長さ前置なし、OS のメッセージ境界に依存。
public interface IIpcClient : IDisposable
{
    // request(serialized protobuf)を送り、response(serialized protobuf)を受け取る。
    // 失敗時は IpcException を送出。
    byte[] Call(byte[] request, TimeSpan timeout);

    Task<byte[]> CallAsync(byte[] request, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public sealed class IpcException : Exception
{
    public IpcException(string message) : base(message) { }
    public IpcException(string message, Exception inner) : base(message, inner) { }

    // 接続確立フェーズ(リクエスト送信前)で「高速に」失敗したことを示す。これが true の
    // ときに限りクライアントは安全に再接続リトライしてよい(server 起動レース等の一時失敗)。
    // 送信開始後の失敗(非冪等コマンドの二重送信リスク)や、タイムアウト後(待機 budget を
    // 消費済み)は false のままにし、リトライ対象から除外する。
    public bool Connecting { get; init; }
}
