using System.IO.Pipes;
using System.Runtime.Versioning;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/win32_ipc.cc の IPCClient(Windows) 相当。
// 名前付きパイプ(メッセージモード)で 1 回の Call ごとに connect→送信→受信→切断。
// フレーミングは長さ前置なし: 送信は 1 メッセージ(WriteFile 相当)、受信はメッセージ完了まで読む。
[SupportedOSPlatform("windows")]
public sealed class NamedPipeIpcClient : IIpcClient
{
    private readonly string _serverName;
    private readonly string _pipeName;

    // pipeName は "\\.\pipe\" を除いた名前(例: "mozc.<key>.session")。
    // IpcPathManager.GetWindowsPipeName() の戻り値をそのまま渡す。
    public NamedPipeIpcClient(string pipeName, string serverName = ".")
    {
        _pipeName = pipeName;
        _serverName = serverName;
    }

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

        using var pipe = new NamedPipeClientStream(
            _serverName, _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync((int)timeout.TotalMilliseconds, cts.Token).ConfigureAwait(false);

            // C++ の SetNamedPipeHandleState(PIPE_READMODE_MESSAGE) 相当。
            // これで受信側はメッセージ境界(IsMessageComplete)に依拠できる。
            pipe.ReadMode = PipeTransmissionMode.Message;

            await pipe.WriteAsync(request, cts.Token).ConfigureAwait(false);

            using var ms = new MemoryStream();
            byte[] buffer = new byte[MozcConstants.IpcInitialReadBufferSize];
            do
            {
                int read = await pipe.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break; // EOF / 切断
                }
                ms.Write(buffer, 0, read);
            }
            while (!pipe.IsMessageComplete);

            return ms.ToArray();
        }
        catch (OperationCanceledException ex)
        {
            throw new IpcException("IPC timed out", ex);
        }
        catch (IOException ex)
        {
            throw new IpcException("IPC I/O error", ex);
        }
        catch (TimeoutException ex)
        {
            throw new IpcException("IPC connect timed out", ex);
        }
    }

    public void Dispose()
    {
        // connect-per-call なので保持リソースなし。
    }
}
