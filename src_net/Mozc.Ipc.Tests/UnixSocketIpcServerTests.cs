using System.Runtime.Versioning;
using Mozc.Ipc;
using Xunit;

namespace Mozc.Ipc.Tests;

// abstract namespace Unix socket は Linux 限定。Windows では skip(Linux CI で実行)。
[SupportedOSPlatform("linux")]
public class UnixSocketIpcServerTests
{
    [Fact]
    public void ClientServer_RoundTrip_Echo()
    {
        if (!global::System.OperatingSystem.IsLinux())
        {
            return; // 非 Linux は対象外
        }
        byte[] name = global::System.Text.Encoding.ASCII.GetBytes(
            "\0mozc.test." + global::System.Guid.NewGuid().ToString("N"));
        using var server = new UnixSocketIpcServer(name, req =>
        {
            var resp = new byte[req.Length];
            for (int i = 0; i < req.Length; i++)
            {
                resp[i] = (byte)(req[i] + 1);
            }
            return resp;
        });
        server.Start();

        var client = new UnixSocketIpcClient(name);
        byte[] response = client.Call(new byte[] { 10, 20, 30 }, global::System.TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[] { 11, 21, 31 }, response);
    }
}
