using Mozc.Base;
using Mozc.Ipc;
using Xunit;

namespace Mozc.Ipc.Tests;

public class IpcPathManagerCreateTests
{
    [Fact]
    public void Create_Then_Load_RoundTrip()
    {
        string name = "test_" + global::System.Guid.NewGuid().ToString("N").Substring(0, 8);
        IpcPathManager server = IpcPathManager.Create(name, processId: 4242, productVersion: "1.2.3.4");

        Assert.True(IpcPathManager.IsValidKey(server.Key));
        Assert.Equal(MozcConstants.IpcProtocolVersion, server.ServerProtocolVersion);

        // クライアント側が .ipc を読み戻す。
        var client = new IpcPathManager();
        Assert.True(client.TryLoad(name));
        Assert.Equal(server.Key, client.Key);
        Assert.Equal(4242u, client.ServerProcessId);
        Assert.Equal("1.2.3.4", client.ServerProductVersion);
        Assert.True(client.IsCompatibleProtocolVersion());
        // パイプ名はサーバ/クライアントで一致(同一 key.name)。
        Assert.Equal(server.GetWindowsPipeName(), client.GetWindowsPipeName());
    }

    [Fact]
    public void Load_NonExistent_ReturnsFalse()
    {
        var m = new IpcPathManager();
        Assert.False(m.TryLoad("nonexistent_" + global::System.Guid.NewGuid().ToString("N")));
    }
}
