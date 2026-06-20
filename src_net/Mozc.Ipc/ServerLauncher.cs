using System.Diagnostics;

namespace Mozc.Ipc;

// C++ src/client/server_launcher.cc 相当(クライアント主導のオンデマンドサーバ起動)。
// .ipc メタデータが無い(= mozc_server 未起動)とき、server 実行ファイルを「引数なし」で spawn し、
// .ipc が公開されるまでポーリングする。C++ の server_status_ ステートマシンに倣い、一度 FATAL に
// なったら以後は再試行しない(失敗のたびに毎キー spawn する respawn storm を防ぐ)。
//
// 移植メモ: native コンパイル/実機 clean install での end-to-end 検証はサンドボックス外
// (物理ブロック)。可用性判定・spawn・待機を注入可能にし、ラッチ/ポーリングのロジックを
// 単体テストで固定する。実 spawn(Process.Start)は薄い既定実装。
public sealed class ServerLauncher
{
    public enum Status { Unknown, Running, Fatal }

    // C++ server_launcher.cc: kTrial=20 回・1 秒間隔で .ipc 公開を待つ。
    private const int DefaultTrials = 20;
    private const int DefaultIntervalMs = 1000;

    private readonly object _lock = new();
    private readonly Func<bool> _isAvailable; // .ipc が公開済みかつワイヤー互換か
    private readonly Func<bool> _spawn;       // server を起動(成功で true)
    private readonly Action _wait;            // ポーリング間隔の待機
    private readonly int _trials;
    private Status _status = Status.Unknown;

    public Status CurrentStatus
    {
        get { lock (_lock) { return _status; } }
    }

    // 本番用: 既定の可用性判定(IpcPathManager.TryLoad + 互換確認)/ spawn(Process.Start)/ 1s 待機。
    // serverPathResolver は server 実行ファイルの絶対パスを返す(null/不在なら spawn 失敗)。
    // 未指定時は実行ファイルと同じディレクトリの Mozc.Server.Host(.exe) / mozc_server を探す。
    public ServerLauncher(string ipcName, Func<string?>? serverPathResolver = null)
        : this(
            isAvailable: () => DefaultIsAvailable(ipcName),
            spawn: () => DefaultSpawn(serverPathResolver),
            wait: () => global::System.Threading.Thread.Sleep(DefaultIntervalMs),
            trials: DefaultTrials)
    {
    }

    // テスト用: 依存を注入する(実プロセス/実ファイルに触れずラッチ・ポーリングを検証する)。
    internal ServerLauncher(Func<bool> isAvailable, Func<bool> spawn, Action wait, int trials)
    {
        _isAvailable = isAvailable;
        _spawn = spawn;
        _wait = wait;
        _trials = trials;
    }

    // server が利用可能になるよう保証する。既に公開済みなら即 true。未起動なら spawn して
    // .ipc 公開を待つ。spawn 失敗 / 待ち切れなかった場合は FATAL にラッチして false(以後即 false)。
    public bool EnsureServerRunning()
    {
        lock (_lock)
        {
            if (_status == Status.Fatal)
            {
                return false; // 一度諦めたら再試行しない(respawn storm 防止)。
            }
            if (_isAvailable())
            {
                _status = Status.Running;
                return true;
            }
            if (!_spawn())
            {
                _status = Status.Fatal;
                return false;
            }
            for (int i = 0; i < _trials; i++)
            {
                if (_isAvailable())
                {
                    _status = Status.Running;
                    return true;
                }
                _wait();
            }
            // 最後の待機後にもう一度だけ確認する。
            if (_isAvailable())
            {
                _status = Status.Running;
                return true;
            }
            _status = Status.Fatal;
            return false;
        }
    }

    private static bool DefaultIsAvailable(string ipcName)
    {
        var pm = new IpcPathManager();
        if (!pm.TryLoad(ipcName) || !pm.IsCompatibleProtocolVersion())
        {
            return false;
        }
        // .ipc に記録された server PID が生存していなければ stale(server がクラッシュ/終了済み)。
        // TryLoad はファイルが残っていれば成功するため、PID 生存確認をしないと死んだ .ipc を
        // 「利用可能」と誤判定し、EnsureServerRunning が永久に spawn せず接続不能になる(C++ も
        // server_launcher が pid 生存を確認して再起動する)。
        return IsServerProcessAlive(pm.ServerProcessId);
    }

    // .ipc を広告する server プロセスが生存しているか(トランスポートの stale 検出にも使う)。
    // 未ロード(ProcessId 既定 0)や pid 未記録は判定不能なので「生存(=respawn 不要)」扱い。
    // 呼び出し側は TryLoad 成功後にこれが false なら EnsureServerRunning で respawn する。
    public static bool IsAdvertisedServerAlive(IpcPathManager pathManager)
        => IsServerProcessAlive(pathManager.ServerProcessId);

    // .ipc を広告する PID のプロセスが「mozc_server 本体」として生存しているか。
    // 単なる PID 生存だけだと、再起動や通常の PID 再利用で .ipc が無関係なプロセスを
    // 指したとき stale を見逃し、死んだソケット/パイプへ接続し続ける。PID 生存に加えて
    // プロセス名が server 実行ファイル名と一致することを確認し、同一性を検証する。
    // pid==0(未記録)は判定不能なので生存扱い(=respawn 不要)。
    private static bool IsServerProcessAlive(uint pid)
    {
        if (pid == 0)
        {
            return true;
        }
        try
        {
            using Process p = Process.GetProcessById((int)pid);
            if (p.HasExited)
            {
                return false;
            }
            // プロセス名が server 実行ファイル(Windows: Mozc.Server.Host / Unix: mozc_server)と
            // 一致するか。一致しなければ PID 再利用された無関係プロセス → stale 扱いで respawn。
            return IsServerProcessName(p.ProcessName);
        }
        catch (global::System.ArgumentException)
        {
            return false; // 該当 PID のプロセスが存在しない = stale。
        }
        catch (global::System.InvalidOperationException)
        {
            return false;
        }
    }

    // ProcessName は拡張子を含まない(Windows "Mozc.Server.Host" / Unix "mozc_server")。
    private static bool IsServerProcessName(string name)
        => string.Equals(name, "Mozc.Server.Host", global::System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "mozc_server", global::System.StringComparison.OrdinalIgnoreCase);

    private static bool DefaultSpawn(Func<string?>? resolver)
    {
        string? exe = (resolver ?? DefaultServerPath)();
        if (string.IsNullOrEmpty(exe) || !global::System.IO.File.Exists(exe))
        {
            return false;
        }
        try
        {
            // C++ 同様、引数なしで起動する(server 自身が同梱データへフォールバックする)。
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? p = Process.Start(psi);
            return p != null;
        }
        catch (global::System.Exception ex) when (
            ex is global::System.ComponentModel.Win32Exception
                or global::System.InvalidOperationException
                or global::System.IO.IOException)
        {
            Mozc.Base.MozcLog.Error("ServerLauncher spawn", ex);
            return false;
        }
    }

    // 既定の server 実行ファイル探索: 呼び出し元アセンブリと同じディレクトリの
    // Mozc.Server.Host(.exe) / mozc_server(C++ SystemUtil::GetServerPath 相当)。
    // インストールレイアウトが分離している場合(deb の ibus エンジンと server が別ディレクトリ等)は
    // 呼び出し側が serverPathResolver を渡してパスを与える。
    private static string? DefaultServerPath()
    {
        string dir = global::System.AppContext.BaseDirectory;
        string exe = global::System.OperatingSystem.IsWindows()
            ? "Mozc.Server.Host.exe"
            : "mozc_server";
        string path = global::System.IO.Path.Combine(dir, exe);
        return global::System.IO.File.Exists(path) ? path : null;
    }
}
