using Mozc.Ipc;
using Mozc.Server;

namespace Mozc.Server.Host;

// mozc_server エントリポイント。EngineServer を IPC(NamedPipe/Unix socket)で公開し常駐する。
// 使い方: Mozc.Server.Host --data mozc.data --roman romanji-hiragana.tsv --keymap atok.tsv [--pipe mozc.session]
internal static class Program
{
    public static int Main(string[] args)
    {
        string data = string.Empty, roman = string.Empty, keymap = string.Empty;
        string pipe = "mozc.session";
        string? dataDir = null;
        string? profileDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : string.Empty;
            switch (args[i])
            {
                case "--data": data = Next(); break;
                case "--roman": roman = Next(); break;
                case "--keymap": keymap = Next(); break;
                case "--pipe": pipe = Next(); break;
                case "--datadir": dataDir = Next(); break;
                case "--profile": profileDir = Next(); break;
            }
        }
        // C++ mozc_server は data/roman/keymap を引数で取らず、インストール済みの同梱データを
        // 内部解決する(mozc_server_main.cc)。C# 版も引数省略時は実行ファイル近傍の同梱ファイルへ
        // フォールバックする。これがないと ServerLauncher による引数なし spawn 後に即終了し、
        // TIP/ibus が接続先を永久に持てない(clean install で変換不能)。
        // 探索先: 実行ファイルディレクトリ(MSI=INSTALLFOLDER / deb=/usr/lib/mozc)と、その隣の
        // ../Resources(macOS .app バンドルは server=Contents/MacOS、data=Contents/Resources)。
        string exeDir = global::System.AppContext.BaseDirectory;
        string Resolve(string file)
        {
            string[] candidates =
            {
                global::System.IO.Path.Combine(exeDir, file),
                global::System.IO.Path.Combine(exeDir, "..", "Resources", file),
            };
            foreach (string c in candidates)
            {
                if (global::System.IO.File.Exists(c))
                {
                    return c;
                }
            }
            return candidates[0]; // 既定(存在チェックは後段)。
        }
        if (data.Length == 0) data = Resolve("mozc.data");
        if (roman.Length == 0) roman = Resolve("roman.tsv");
        if (keymap.Length == 0) keymap = Resolve("keymap.tsv");
        // keymap プリセット(<datadir>/keymap/<preset>.tsv)・記号/単漢字の実データも同梱
        // ディレクトリから解決できるよう、--datadir 未指定なら解決した mozc.data の所在を使う。
        dataDir ??= global::System.IO.Path.GetDirectoryName(global::System.IO.Path.GetFullPath(data)) ?? exeDir;

        if (!global::System.IO.File.Exists(data)
            || !global::System.IO.File.Exists(roman)
            || !global::System.IO.File.Exists(keymap))
        {
            global::System.Console.Error.WriteLine(
                $"required data not found: data='{data}' roman='{roman}' keymap='{keymap}' " +
                "(pass --data/--roman/--keymap, or bundle them next to the executable)");
            return 2;
        }

        // --datadir 指定時は symbol.tsv/single_kanji.tsv を実データ結線する。
        EngineServer server = ServerHost.Create(data, roman, keymap, dataDir);

        // プロファイル(設定/履歴/ユーザー辞書)を起動時 load、終了時 save。
        // --profile 未指定時は OS 標準のユーザープロファイルディレクトリを使う。
        string resolvedProfile = profileDir ?? ServerHost.DefaultProfileDir();
        ServerHost.LoadProfile(server, resolvedProfile);
        global::System.AppDomain.CurrentDomain.ProcessExit +=
            (_, _) => ServerHost.SaveProfile(server, resolvedProfile);
        // SYNC_DATA(クライアントの定期/終了フラッシュ要求)で学習データを永続化する。
        server.OnSyncData = () => ServerHost.SaveHistoryOnly(server, resolvedProfile);

        // .ipc を公開(key/protocol/pid)。クライアントは IpcPathManager.TryLoad で実 pipe 名を得る。
        IpcPathManager pathManager = IpcPathManager.Create(
            pipe, (uint)global::System.Environment.ProcessId);
        string actualPipe = global::System.OperatingSystem.IsWindows()
            ? pathManager.GetWindowsPipeName()
            : pipe;

        if (global::System.OperatingSystem.IsWindows())
        {
            using var ipc = new NamedPipeIpcServer(actualPipe, server.HandleProtoRequest);
            ipc.Start();
            global::System.Console.WriteLine($"mozc_server (C#) listening on pipe '{pipe}'. Ctrl+C to stop.");
            WaitForever();
        }
        else if (global::System.OperatingSystem.IsLinux())
        {
            // .ipc に広告した鍵入りの abstract socket 名にバインドする
            // (クライアントは IpcPathManager から同じ名前を導出して接続する)。
            byte[] name = pathManager.GetLinuxAbstractSocketName();
            using var ipc = new UnixSocketIpcServer(name, server.HandleProtoRequest);
            ipc.Start();
            global::System.Console.WriteLine($"mozc_server (C#) listening on abstract socket '{pipe}'. Ctrl+C to stop.");
            WaitForever();
        }
        else
        {
            // macOS は abstract socket 非対応 → ファイルシステム UDS にバインドする。
            string socketPath = pathManager.GetFileSocketPath();
            using var ipc = new FileSocketIpcServer(socketPath, server.HandleProtoRequest);
            ipc.Start();
            global::System.Console.WriteLine($"mozc_server (C#) listening on file socket '{socketPath}'. Ctrl+C to stop.");
            WaitForever();
        }
        return 0;
    }

    private static void WaitForever()
    {
        using var done = new global::System.Threading.ManualResetEventSlim(false);
        global::System.Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
        done.Wait();
    }
}
