using System.IO.Pipes;
using Google.Protobuf;
using Mozc.Base;
using Mozc.Commands;
using Xunit;

namespace Mozc.Ipc.Tests;

// Windows 名前付きパイプ(メッセージモード)の往復を、ループバックの
// NamedPipeServerStream で検証する。protobuf Input→Output の実フローを再現。
public class NamedPipeIpcClientTests
{
    [Fact]
    public async Task Call_RoundTripsProtobufOverMessagePipe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // named pipe IPC client は Windows 専用
        }

        string pipeName = "mozc.test." + Guid.NewGuid().ToString("N");

        using var server = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Message, PipeOptions.Asynchronous);

        // サーバ側: 接続待ち→Input を 1 メッセージ受信→同 id の Output を返す。
        Task serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();

            using var ms = new MemoryStream();
            byte[] buf = new byte[MozcConstants.IpcInitialReadBufferSize];
            do
            {
                int n = await server.ReadAsync(buf);
                if (n <= 0) break;
                ms.Write(buf, 0, n);
            }
            while (!server.IsMessageComplete);

            var input = Input.Parser.ParseFrom(ms.ToArray());
            var output = new Output { Id = input.Id, Consumed = true };
            byte[] reply = output.ToByteArray();
            await server.WriteAsync(reply);
            await server.FlushAsync();
        });

        var client = new NamedPipeIpcClient(pipeName);
        var request = new Input { Type = Input.Types.CommandType.NoOperation, Id = 12345 };
        byte[] respBytes = await client.CallAsync(request.ToByteArray(), TimeSpan.FromSeconds(10));

        var response = Output.Parser.ParseFrom(respBytes);
        Assert.Equal(12345ul, response.Id);
        Assert.True(response.Consumed);

        await serverTask;
    }
}
