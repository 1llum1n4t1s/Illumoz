using System.IO.Pipes;
using System.Runtime.Versioning;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/win32_ipc.cc の IPCServer(Windows) 相当。
// 名前付きパイプ(メッセージモード)で接続を受け、1 メッセージ受信→ハンドラ→応答送信→切断を繰り返す。
// ハンドラは byte[] リクエスト → byte[] レスポンス(EngineServer.HandleRequest を渡す想定)。
[SupportedOSPlatform("windows")]
public sealed class NamedPipeIpcServer : IDisposable
{
    private readonly string _pipeName;
    private readonly Func<byte[], byte[]> _handler;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public NamedPipeIpcServer(string pipeName, Func<byte[], byte[]> handler)
    {
        _pipeName = pipeName;
        _handler = handler;
    }

    public void Start()
    {
        _loop ??= Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

                byte[] request = await ReadMessageAsync(pipe, token).ConfigureAwait(false);
                if (request.Length > 0)
                {
                    byte[] response = _handler(request);
                    await pipe.WriteAsync(response, token).ConfigureAwait(false);
                    pipe.WaitForPipeDrain();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // クライアント切断等。次の接続へ。
            }
            catch (Exception)
            {
                // ハンドラ等の予期せぬ例外で受付ループが死ぬのを防ぐ。
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private static async Task<byte[]> ReadMessageAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using var ms = new MemoryStream();
        byte[] buffer = new byte[MozcConstants.IpcInitialReadBufferSize];
        do
        {
            int read = await pipe.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }
            ms.Write(buffer, 0, read);
        }
        while (!pipe.IsMessageComplete);
        return ms.ToArray();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // キャンセル由来は無視。
        }
        _cts.Dispose();
    }
}
