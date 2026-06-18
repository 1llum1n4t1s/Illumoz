using System.Text;
using Google.Protobuf;
using Mozc.Base;
using Xunit;

namespace Mozc.Ipc.Tests;

public class IpcPathManagerTests : IDisposable
{
    private readonly string _tempDir;

    public IpcPathManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mozc_ipc_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        MozcPaths.OverrideUserProfileDirectory(_tempDir);
    }

    public void Dispose()
    {
        MozcPaths.OverrideUserProfileDirectory(null);
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)] // 32 hex
    [InlineData("0123456789ABCDEF0123456789ABCDEF", false)] // 大文字は不可
    [InlineData("0123456789abcdef", false)] // 短い
    [InlineData("0123456789abcdef0123456789abcdeg", false)] // g は hex でない
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidKey_Validates(string? key, bool expected)
        => Assert.Equal(expected, IpcPathManager.IsValidKey(key));

    [Fact]
    public void TryLoad_ReadsIpcPathInfo_FromFile()
    {
        // C++ サーバが書く .ipc と同形式(IPCPathInfo binary)をテストで生成。
        const string name = "session";
        const string key = "0123456789abcdef0123456789abcdef";
        var info = new IPCPathInfo
        {
            Key = key,
            ProtocolVersion = MozcConstants.IpcProtocolVersion,
            ProductVersion = "2.30.1234.0",
            ProcessId = 4242,
        };
        File.WriteAllBytes(MozcPaths.GetIpcKeyFileName(name), info.ToByteArray());

        var mgr = new IpcPathManager();
        Assert.True(mgr.TryLoad(name));
        Assert.Equal(key, mgr.Key);
        Assert.Equal(MozcConstants.IpcProtocolVersion, mgr.ServerProtocolVersion);
        Assert.Equal("2.30.1234.0", mgr.ServerProductVersion);
        Assert.Equal(4242u, mgr.ServerProcessId);
        Assert.True(mgr.IsCompatibleProtocolVersion());
    }

    [Fact]
    public void TryLoad_RejectsInvalidKey()
    {
        const string name = "session";
        var info = new IPCPathInfo { Key = "too-short", ProtocolVersion = 3 };
        File.WriteAllBytes(MozcPaths.GetIpcKeyFileName(name), info.ToByteArray());

        var mgr = new IpcPathManager();
        Assert.False(mgr.TryLoad(name));
    }

    [Fact]
    public void TryLoad_ReturnsFalse_WhenMissing()
        => Assert.False(new IpcPathManager().TryLoad("nonexistent"));

    [Fact]
    public void GetWindowsPipeName_HasExpectedFormat()
    {
        const string name = "session";
        const string key = "0123456789abcdef0123456789abcdef";
        var info = new IPCPathInfo { Key = key, ProtocolVersion = 3 };
        File.WriteAllBytes(MozcPaths.GetIpcKeyFileName(name), info.ToByteArray());

        var mgr = new IpcPathManager();
        Assert.True(mgr.TryLoad(name));
        // "\\.\pipe\" を除いた .NET パイプ名
        Assert.Equal($"mozc.{key}.session", mgr.GetWindowsPipeName());
    }

    [Fact]
    public void GetLinuxAbstractSocketName_StartsWithNul_AndMatchesCxxLayout()
    {
        const string name = "session";
        const string key = "0123456789abcdef0123456789abcdef";
        var info = new IPCPathInfo { Key = key, ProtocolVersion = 3 };
        File.WriteAllBytes(MozcPaths.GetIpcKeyFileName(name), info.ToByteArray());

        var mgr = new IpcPathManager();
        Assert.True(mgr.TryLoad(name));
        byte[] addr = mgr.GetLinuxAbstractSocketName();

        // 先頭は '\0'、残りは "tmp/.mozc.<key>.<name>" ('/tmp...' の先頭 '/' が '\0' 化)
        Assert.Equal(0, addr[0]);
        string rest = Encoding.ASCII.GetString(addr, 1, addr.Length - 1);
        Assert.Equal($"tmp/.mozc.{key}.{name}", rest);
    }
}
