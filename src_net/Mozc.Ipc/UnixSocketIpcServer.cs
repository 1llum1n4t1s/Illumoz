using System.Net.Sockets;
using System.Runtime.Versioning;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/unix_ipc.cc の IPCServer(Linux) 相当。
// abstract namespace の Unix domain socket を bind/listen し、接続ごとに
// EOF(クライアントの SHUT_WR)まで受信 → ハンドラ → 応答送信 → close を行う。
// 【並行モデル】単一 accept ループで1接続ずつ逐次処理する。SessionHandler/EngineServer は
// 共有可変状態を持ちスレッドセーフでないため直列性が前提。並行受付化にはロック設計が必須。
[SupportedOSPlatform("linux")]
public sealed class UnixSocketIpcServer : IDisposable
{
    private readonly byte[] _abstractName;
    private readonly Func<byte[], byte[]> _handler;
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;
    private Task? _loop;

    // IpcPathManager.GetLinuxAbstractSocketName() の戻り値(先頭 '\0' 含む)を渡す。
    public UnixSocketIpcServer(byte[] abstractSocketName, Func<byte[], byte[]> handler)
    {
        _abstractName = abstractSocketName;
        _handler = handler;
    }

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new AbstractUnixEndPoint(_abstractName));
        _listener.Listen(backlog: 16);
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
            catch (SocketException ex)
            {
                // 接続エラー。次へ。
                Mozc.Base.MozcLog.Error("UnixSocket accept", ex);
            }
            catch (Exception ex)
            {
                // ハンドラ・リクエスト過大等の予期せぬ例外で受付ループが死ぬのを防ぐ。
                Mozc.Base.MozcLog.Error("UnixSocket handler", ex);
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
                break; // クライアントが SHUT_WR で半クローズ
            }
            ms.Write(buffer, 0, read);
            // 上限超過(破損/悪意の巨大リクエスト)は読み取りを打ち切り破棄する(OOM 防止)。
            if (ms.Length > MozcConstants.IpcMaxRequestSize)
            {
                throw new IOException("IPC request exceeds max size");
            }
        }
        return ms.ToArray();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _cts.Dispose();
    }
}
