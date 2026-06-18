using System.Runtime.Versioning;
using Mozc.Ipc;
using Xunit;

namespace Mozc.Ipc.Tests;

[SupportedOSPlatform("windows")]
public class NamedPipeIpcServerTests
{
    private static string UniquePipeName()
        => "mozc.test." + global::System.Guid.NewGuid().ToString("N");

    [Fact]
    public void ClientServer_RoundTrip_Echo()
    {
        if (!global::System.OperatingSystem.IsWindows())
        {
            return;
        }
        string pipeName = UniquePipeName();
        // ハンドラ: リクエストの各バイトに +1 して返す。
        using var server = new NamedPipeIpcServer(pipeName, req =>
        {
            var resp = new byte[req.Length];
            for (int i = 0; i < req.Length; i++)
            {
                resp[i] = (byte)(req[i] + 1);
            }
            return resp;
        });
        server.Start();

        var client = new NamedPipeIpcClient(pipeName);
        byte[] response = client.Call(new byte[] { 10, 20, 30 }, global::System.TimeSpan.FromSeconds(5));

        Assert.Equal(new byte[] { 11, 21, 31 }, response);
    }

    [Fact]
    public void ClientServer_MultipleSequentialCalls()
    {
        if (!global::System.OperatingSystem.IsWindows())
        {
            return;
        }
        string pipeName = UniquePipeName();
        using var server = new NamedPipeIpcServer(pipeName, req => req); // echo
        server.Start();

        var client = new NamedPipeIpcClient(pipeName);
        for (int i = 1; i <= 3; i++)
        {
            byte[] response = client.Call(new byte[] { (byte)i }, global::System.TimeSpan.FromSeconds(5));
            Assert.Equal(new byte[] { (byte)i }, response);
        }
    }
}
