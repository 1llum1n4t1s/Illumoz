using System.Net;
using System.Net.Sockets;

namespace Mozc.Ipc;

// Linux の abstract namespace Unix domain socket 用 EndPoint。
// 先頭バイトが '\0' のアドレス(例: "\0tmp/.mozc.<key>.<name>")を扱う。
// .NET 標準の UnixDomainSocketEndPoint は先頭 '\0' を正しく扱えないため自前実装。
// SocketAddress レイアウト: [0..1]=sa_family(AF_UNIX), [2..]=sun_path(先頭 '\0' 含む)。
internal sealed class AbstractUnixEndPoint : EndPoint
{
    private readonly byte[] _name; // 先頭 '\0' を含むアドレス名

    public AbstractUnixEndPoint(byte[] name) => _name = name;

    public override AddressFamily AddressFamily => AddressFamily.Unix;

    public override SocketAddress Serialize()
    {
        // 先頭 2 バイトは family。残りに abstract 名(先頭 '\0' 含む)をそのまま入れる。
        var sa = new SocketAddress(AddressFamily.Unix, 2 + _name.Length);
        for (int i = 0; i < _name.Length; i++)
        {
            sa[2 + i] = _name[i];
        }
        return sa;
    }

    public override EndPoint Create(SocketAddress socketAddress) => this;
}
