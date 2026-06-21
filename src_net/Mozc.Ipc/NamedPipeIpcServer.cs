using System.IO.Pipes;
using System.Runtime.Versioning;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/win32_ipc.cc の IPCServer(Windows) 相当。
// 名前付きパイプ(メッセージモード)で接続を受け、1 メッセージ受信→ハンドラ→応答送信→切断を繰り返す。
// ハンドラは byte[] リクエスト → byte[] レスポンス(EngineServer.HandleRequest を渡す想定)。
// 【並行モデル】単一 accept ループで1接続ずつ逐次処理する(handler を await し終えてから次を accept)。
// SessionHandler/EngineServer は共有可変状態を持ちスレッドセーフでないため、この直列性が前提。
// 並行受付化する場合は SessionHandler 側のロック設計が必須。
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
                    // メッセージモードでは Write 完了で受信側がメッセージ境界を検出できるため
                    // WaitForPipeDrain は不要(同期ブロックを避ける)。
                    await pipe.WriteAsync(response, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                // クライアント切断・リクエスト過大等。次の接続へ。
                MozcLog.Error("NamedPipe accept", ex);
            }
            catch (Exception ex)
            {
                // ハンドラ等の予期せぬ例外で受付ループが死ぬのを防ぐ。
                MozcLog.Error("NamedPipe handler", ex);
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
            // 上限超過(破損/悪意の巨大リクエスト)は読み取りを打ち切り破棄する(OOM 防止)。
            if (ms.Length > MozcConstants.IpcMaxRequestSize)
            {
                throw new IOException("IPC request exceeds max size");
            }
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
