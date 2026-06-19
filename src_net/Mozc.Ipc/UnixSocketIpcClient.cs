using System.Net.Sockets;
using System.Runtime.Versioning;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/unix_ipc.cc の IPCClient(Linux) 相当。
// abstract namespace の Unix domain socket に接続し、
// 送信→shutdown(SHUT_WR) で半クローズ→EOF まで受信(長さ前置なし)。
[SupportedOSPlatform("linux")]
public sealed class UnixSocketIpcClient : IIpcClient
{
    private readonly byte[] _abstractName; // 先頭 '\0' を含む abstract socket 名

    // IpcPathManager.GetLinuxAbstractSocketName() の戻り値を渡す。
    public UnixSocketIpcClient(byte[] abstractSocketName) => _abstractName = abstractSocketName;

    public byte[] Call(byte[] request, TimeSpan timeout)
        => CallAsync(request, timeout).GetAwaiter().GetResult();

    public async Task<byte[]> CallAsync(byte[] request, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (request.Length == 0)
        {
            throw new IpcException("request is empty");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new AbstractUnixEndPoint(_abstractName), cts.Token).ConfigureAwait(false);

            // SendAsync は部分送信し得るため全バイト送るまでループする。
            int sent = 0;
            while (sent < request.Length)
            {
                int n = await socket.SendAsync(
                    request.AsMemory(sent), SocketFlags.None, cts.Token).ConfigureAwait(false);
                if (n <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                sent += n;
            }

            // C++: shutdown(SHUT_WR)。これがないとサーバが request 長を判定できずタイムアウトする。
            socket.Shutdown(SocketShutdown.Send);

            using var ms = new MemoryStream();
            byte[] buffer = new byte[MozcConstants.IpcInitialReadBufferSize];
            while (true)
            {
                int read = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break; // EOF(サーバが応答後に close)
                }
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
        catch (OperationCanceledException ex)
        {
            throw new IpcException("IPC timed out", ex);
        }
        catch (SocketException ex)
        {
            throw new IpcException("IPC socket error", ex);
        }
    }

    public void Dispose()
    {
        // connect-per-call なので保持リソースなし。
    }
}
