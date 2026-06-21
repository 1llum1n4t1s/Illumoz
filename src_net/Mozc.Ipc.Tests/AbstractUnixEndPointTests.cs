using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using Mozc.Base;
using Xunit;

namespace Mozc.Ipc.Tests;

// Linux abstract socket アドレスの SocketAddress レイアウトを OS 非依存で検証。
// (実接続は Linux 専用のため統合テストは別途。ここは最も間違えやすい
//  アドレス構築 = family + 先頭 '\0' + 名前 の正しさを固定する。)
public class AbstractUnixEndPointTests
{
    [Fact]
    public void Serialize_HasFamilyNulPrefixAndName()
    {
        const string name = "session";
        const string key = "0123456789abcdef0123456789abcdef";
        var info = new IPCPathInfo { Key = key, ProtocolVersion = 3 };

        string tempDir = Path.Combine(Path.GetTempPath(), "mozc_aue_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        MozcPaths.OverrideUserProfileDirectory(tempDir);
        try
        {
            File.WriteAllBytes(MozcPaths.GetIpcKeyFileName(name), info.ToByteArray());
            var mgr = new IpcPathManager();
            Assert.True(mgr.TryLoad(name));
            byte[] abstractName = mgr.GetLinuxAbstractSocketName();

            var ep = new AbstractUnixEndPoint(abstractName);
            SocketAddress sa = ep.Serialize();

            Assert.Equal(AddressFamily.Unix, sa.Family);
            Assert.Equal(2 + abstractName.Length, sa.Size);
            // sun_path 先頭は '\0' (abstract namespace)
            Assert.Equal(0, sa[2]);
            // 残りは "tmp/.mozc.<key>.<name>"
            var rest = new byte[abstractName.Length - 1];
            for (int i = 0; i < rest.Length; i++)
            {
                rest[i] = sa[2 + 1 + i];
            }
            Assert.Equal($"tmp/.mozc.{key}.{name}", Encoding.ASCII.GetString(rest));
        }
        finally
        {
            MozcPaths.OverrideUserProfileDirectory(null);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
