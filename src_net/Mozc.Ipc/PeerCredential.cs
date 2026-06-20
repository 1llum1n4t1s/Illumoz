using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Mozc.Ipc;

// 接続済み Unix domain socket の相手(クライアント)プロセスの uid を取得し、自プロセスの
// 実効 uid と一致するかを検証する。C++ 本家 unix_ipc.cc が getsockopt(SO_PEERCRED) で
// 行う peer 認証の移植。abstract socket(Linux)はファイルパーミッションが効かず、socket 名は
// 機密でないため、別ユーザのなりすまし接続を弾く最終防御として uid 検証が必要。
//
// NativeAOT 安全(LibraryImport ソース生成、リフレクション不使用)。
internal static partial class PeerCredential
{
    private const int SolSocket = 1;   // Linux SOL_SOCKET
    private const int SoPeercred = 17; // Linux SO_PEERCRED

    // 相手 uid が自 euid と一致すれば true。判定対象外OS(Windows 等)や、稀な syscall 失敗時は
    // 機能を壊さないため true(=許可)を返し reason に理由を入れる(確実な別uidのみ false で弾く)。
    public static bool IsSameUser(Socket conn, out string reason)
    {
        try
        {
            int fd = (int)conn.Handle;
            if (OperatingSystem.IsLinux())
            {
                // struct ucred { pid_t pid; uid_t uid; uid_t gid; } = int+uint+uint = 12 bytes。
                Span<byte> buf = stackalloc byte[12];
                int len = buf.Length;
                int rc = LinuxGetsockopt(fd, SolSocket, SoPeercred,
                    ref MemoryMarshal.GetReference(buf), ref len);
                if (rc != 0)
                {
                    reason = "SO_PEERCRED unavailable";
                    return true;
                }
                uint peerUid = BitConverter.ToUInt32(buf.Slice(4, 4)); // pid(4) の次が uid(4)
                uint euid = Geteuid();
                reason = $"peer uid={peerUid} euid={euid}";
                return peerUid == euid;
            }
            if (OperatingSystem.IsMacOS())
            {
                int rc = MacGetpeereid(fd, out uint peerUid, out uint _);
                if (rc != 0)
                {
                    reason = "getpeereid unavailable";
                    return true;
                }
                uint euid = Geteuid();
                reason = $"peer uid={peerUid} euid={euid}";
                return peerUid == euid;
            }
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return true;
        }
        reason = "not applicable";
        return true;
    }

    [LibraryImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
    private static partial int LinuxGetsockopt(int sockfd, int level, int optname, ref byte optval, ref int optlen);

    [LibraryImport("libc", EntryPoint = "getpeereid", SetLastError = true)]
    private static partial int MacGetpeereid(int s, out uint euid, out uint egid);

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint Geteuid();
}
