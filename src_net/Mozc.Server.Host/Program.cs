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
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : string.Empty;
            switch (args[i])
            {
                case "--data": data = Next(); break;
                case "--roman": roman = Next(); break;
                case "--keymap": keymap = Next(); break;
                case "--pipe": pipe = Next(); break;
            }
        }
        if (data.Length == 0 || roman.Length == 0 || keymap.Length == 0)
        {
            global::System.Console.Error.WriteLine("--data, --roman, --keymap are required");
            return 2;
        }

        EngineServer server = ServerHost.Create(data, roman, keymap);

        if (global::System.OperatingSystem.IsWindows())
        {
            using var ipc = new NamedPipeIpcServer(pipe, server.HandleProtoRequest);
            ipc.Start();
            global::System.Console.WriteLine($"mozc_server (C#) listening on pipe '{pipe}'. Ctrl+C to stop.");
            WaitForever();
        }
        else if (global::System.OperatingSystem.IsLinux())
        {
            byte[] name = global::System.Text.Encoding.ASCII.GetBytes("\0" + pipe);
            using var ipc = new UnixSocketIpcServer(name, server.HandleProtoRequest);
            ipc.Start();
            global::System.Console.WriteLine($"mozc_server (C#) listening on abstract socket '{pipe}'. Ctrl+C to stop.");
            WaitForever();
        }
        else
        {
            // macOS は abstract socket 非対応 → Mach OOL or filesystem UDS transport を別途(後続)。
            global::System.Console.Error.WriteLine("unsupported platform for IPC server transport");
            return 3;
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
