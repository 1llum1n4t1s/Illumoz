using System.Net.Sockets;
using Mozc.Base;

namespace Mozc.Ipc;

// ファイルパス方式の Unix domain socket(AF_UNIX)。.NET 標準 UnixDomainSocketEndPoint は
// 全 OS(Windows10+/macOS/Linux)対応。abstract namespace 非対応の macOS の transport に使う
// (Linux の abstract socket は UnixSocketIpcServer、Windows は NamedPipe を主に使う)。
// framing: client が送信→SHUT_WR で半クローズ→server が EOF まで受信→応答→close。
public sealed class FileSocketIpcServer : IDisposable
{
    private readonly string _path;
    private readonly Func<byte[], byte[]> _handler;
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;
    private Task? _loop;

    public FileSocketIpcServer(string socketPath, Func<byte[], byte[]> handler)
    {
        _path = socketPath;
        _handler = handler;
    }

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }
        if (File.Exists(_path))
        {
            File.Delete(_path); // 残骸 socket ファイルを除去
        }
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_path));
        _listener.Listen(16);
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Socket? conn = null;
            try
            {
                conn = await _listener!.AcceptAsync(token).ConfigureAwait(false);
                byte[] request = await ReadToEofAsync(conn, token).ConfigureAwait(false);
                if (request.Length > 0)
                {
                    byte[] response = _handler(request);
                    // SendAsync は部分送信し得るため全バイト送るまでループする。
                    int sent = 0;
                    while (sent < response.Length)
                    {
                        int n = await conn.SendAsync(
                            response.AsMemory(sent), SocketFlags.None, token).ConfigureAwait(false);
                        if (n <= 0)
                        {
                            throw new SocketException((int)SocketError.ConnectionReset);
                        }
                        sent += n;
                    }
                    conn.Shutdown(SocketShutdown.Send);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                // 接続エラー。次へ。
            }
            catch (Exception)
            {
                // ハンドラ等の予期せぬ例外で受付ループが死ぬのを防ぐ。
            }
            finally
            {
                conn?.Dispose();
            }
        }
    }

    private static async Task<byte[]> ReadToEofAsync(Socket socket, CancellationToken token)
    {
        using var ms = new MemoryStream();
        byte[] buffer = new byte[MozcConstants.IpcInitialReadBufferSize];
        while (true)
        {
            int read = await socket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Dispose(); } catch (ObjectDisposedException) { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        try { if (File.Exists(_path)) File.Delete(_path); } catch (IOException) { }
        _cts.Dispose();
    }
}

// ファイルパス UDS クライアント(connect→送信→SHUT_WR→EOF まで受信)。
public sealed class FileSocketIpcClient : IIpcClient
{
    private readonly string _path;

    public FileSocketIpcClient(string socketPath) => _path = socketPath;

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
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_path), cts.Token).ConfigureAwait(false);
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
            socket.Shutdown(SocketShutdown.Send);

            using var ms = new MemoryStream();
            byte[] buffer = new byte[MozcConstants.IpcInitialReadBufferSize];
            while (true)
            {
                int read = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
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
    }
}
