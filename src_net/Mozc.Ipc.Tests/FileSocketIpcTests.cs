using Mozc.Ipc;
using Xunit;

namespace Mozc.Ipc.Tests;

// ファイルパス UDS(AF_UNIX)は全 OS 対応 → ヘッドレスでも client/server 疎通を検証できる。
public class FileSocketIpcTests
{
    private static string TempSocketPath()
        => global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), "mozc_" + global::System.Guid.NewGuid().ToString("N") + ".sock");

    [Fact]
    public void ClientServer_RoundTrip_Echo()
    {
        string path = TempSocketPath();
        using var server = new FileSocketIpcServer(path, req =>
        {
            var resp = new byte[req.Length];
            for (int i = 0; i < req.Length; i++)
            {
                resp[i] = (byte)(req[i] + 1);
            }
            return resp;
        });
        server.Start();

        var client = new FileSocketIpcClient(path);
        byte[] response = client.Call(new byte[] { 10, 20, 30 }, global::System.TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[] { 11, 21, 31 }, response);
    }

    [Fact]
    public void MultipleSequentialCalls()
    {
        string path = TempSocketPath();
        using var server = new FileSocketIpcServer(path, req => req); // echo
        server.Start();
        var client = new FileSocketIpcClient(path);
        for (int i = 1; i <= 3; i++)
        {
            byte[] r = client.Call(new byte[] { (byte)i }, global::System.TimeSpan.FromSeconds(5));
            Assert.Equal(new byte[] { (byte)i }, r);
        }
    }
}
