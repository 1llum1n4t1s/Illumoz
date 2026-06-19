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
        if (data.Length == 0 || roman.Length == 0 || keymap.Length == 0)
        {
            global::System.Console.Error.WriteLine("--data, --roman, --keymap are required");
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
