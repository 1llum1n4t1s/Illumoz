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
}
