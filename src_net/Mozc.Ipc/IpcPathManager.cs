using System.Text;
using Google.Protobuf;
using Mozc.Base;

namespace Mozc.Ipc;

// C++ src/ipc/ipc_path_manager.cc 相当。
// <UserProfileDir>/(""|".")<name>.ipc を読み、IPCPathInfo(protobuf binary) を parse し、
// key/protocol_version/product_version/process_id を公開する。
// さらに OS 別の名前付きパイプ名 / abstract socket 名を組み立てる。
// 注意: IPCPathInfo 型は生成 protobuf (package mozc.ipc → 名前空間 Mozc.Ipc)。
public sealed class IpcPathManager
{
    private const int MaxIpcFileSize = 2096; // ipc_path_manager.cc: kMaxFileSize

    private IPCPathInfo _info = new();
    private string _name = string.Empty;

    public string Name => _name;
    public string Key => _info.Key ?? string.Empty;
    public uint ServerProtocolVersion => _info.ProtocolVersion;
    public string ServerProductVersion => _info.ProductVersion ?? "0.0.0.0";
    public uint ServerProcessId => _info.ProcessId;

    // ipc_path_manager.cc: IsValidKey。長さ 32、文字は [0-9a-f] のみ。
    public static bool IsValidKey(string? key)
    {
        if (key is null || key.Length != MozcConstants.IpcKeySize)
        {
            return false;
        }
        foreach (char c in key)
        {
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }

    // サーバ側: ランダム 32hex key + protocol/product/pid で .ipc を生成し公開する
    // (ipc_path_manager.cc の CreateNewPathName + SavePathName 相当)。
    // 起動時にこれを呼び、クライアントが TryLoad で読む。
    public static IpcPathManager Create(string name, uint processId, string productVersion = "1.0.0.0")
    {
        IpcPathManager pm = Build(name, processId, productVersion);
        pm.Publish();
        return pm;
    }

    // メタデータ(鍵/protocol/pid)だけをメモリ上に生成する(.ipc ファイルは書かない)。
    // クライアントは .ipc の存在+生存 PID を「サーバ利用可能」とみなすため、リスナーを bind/start
    // してから Publish() で公開する必要がある。先に書くと、クライアントが listener 生成前に接続して
    // 最初のキーイベントを取りこぼす(OS ブリッジは MozcSessionClient の接続リトライを使わない)。
    public static IpcPathManager Build(string name, uint processId, string productVersion = "1.0.0.0")
    {
        var info = new IPCPathInfo
        {
            Key = GenerateKey(),
            ProtocolVersion = MozcConstants.IpcProtocolVersion,
            ProductVersion = productVersion,
            ProcessId = processId,
        };
        return new IpcPathManager { _name = name, _info = info };
    }

    // 保持しているメタデータを .ipc ファイルへ書き出して公開する(SavePathName 相当)。
    // リスナーが listen 状態になった後に呼ぶこと。
    public void Publish()
    {
        string path = MozcPaths.GetIpcKeyFileName(_name);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        byte[] payload = _info.ToByteArray();
        // .ipc は pipe/socket 名を導出する鍵を含むため、書き込み「前」から所有者のみ
        // 読取可で作成する。WriteAllBytes 後に chmod すると、その隙に他ユーザが
        // world-readable な鍵を読める競合があるため、UnixCreateMode で原子的に 0600 にする。
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(path, payload);
        }
        else
        {
            // 既存ファイルは UnixCreateMode が効かない(作成時のみ適用)ため、
            // 緩い権限のまま残らないよう一旦削除してから作り直す。
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var opts = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            };
            using var fs = new FileStream(path, opts);
            fs.Write(payload, 0, payload.Length);
        }
    }

    private static string GenerateKey()
    {
        byte[] bytes = new byte[MozcConstants.IpcKeySize / 2];
        global::System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return global::System.Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // .ipc ファイルを読み込み IPCPathInfo を parse。key を検証して保持。
    // 成功時 true。ファイル不在/壊れ/キー不正は false。
    public bool TryLoad(string name)
    {
        _name = name;
        string path = MozcPaths.GetIpcKeyFileName(name);
        if (!File.Exists(path))
        {
            return false;
        }

        byte[] buf;
        try
        {
            // サーバが FILE_FLAG_DELETE_ON_CLOSE 等で保持しているため共有読みで開く。
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long size = fs.Length;
            if (size <= 0 || size >= MaxIpcFileSize)
            {
                return false;
            }
            buf = new byte[size];
            int read = 0;
            while (read < buf.Length)
            {
                int n = fs.Read(buf, read, buf.Length - read);
                if (n <= 0)
                {
                    break;
                }
                read += n;
            }
            if (read != buf.Length)
            {
                return false;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        IPCPathInfo info;
        try
        {
            info = IPCPathInfo.Parser.ParseFrom(buf);
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }

        if (!IsValidKey(info.Key))
        {
            return false;
        }

        _info = info;
        return true;
    }

    // サーバのプロトコルバージョンが C# 実装と一致するか。
    public bool IsCompatibleProtocolVersion()
        => ServerProtocolVersion == MozcConstants.IpcProtocolVersion;

    // ipc_path_manager.cc: GetPathName (Windows)。
    // .NET NamedPipeClientStream に渡すパイプ名("\\.\pipe\" を除く): "mozc.<key>.<name>"
    public string GetWindowsPipeName()
    {
        EnsureKey();
        return $"{MozcConstants.WindowsPipeNamePrefix}{Key}.{_name}";
    }

    // ipc_path_manager.cc: GetPathName (Linux)。
    // abstract namespace の sun_path: 先頭バイト '\0' + "tmp/.mozc.<key>.<name>"。
    // C++ は "/tmp/.mozc." の先頭 '/' を '\0' に置換する。
    public byte[] GetLinuxAbstractSocketName()
    {
        EnsureKey();
        string body = $"{MozcConstants.PosixIpcPrefix}{Key}.{_name}"; // "/tmp/.mozc.<key>.<name>"
        byte[] bytes = Encoding.ASCII.GetBytes(body);
        bytes[0] = 0; // 先頭 '/' → '\0' (abstract namespace)
        return bytes;
    }

    // macOS 等 abstract socket 非対応プラットフォーム用のファイルシステム UDS パス。
    // "/tmp/.mozc.<key>.<name>"(先頭 '/' を残す通常のパス)。サーバ/クライアントが
    // 同じ .ipc メタデータから同一パスを導出できる。
    public string GetFileSocketPath()
    {
        EnsureKey();
        return $"{MozcConstants.PosixIpcPrefix}{Key}.{_name}";
    }

    private void EnsureKey()
    {
        if (string.IsNullOrEmpty(Key))
        {
            throw new InvalidOperationException("IPC key is empty; call TryLoad first.");
        }
    }
}
