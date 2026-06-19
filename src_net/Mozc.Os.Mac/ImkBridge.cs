using System.Runtime.InteropServices;
using System.Text;
using Pb = Mozc.Commands;

namespace Mozc.Os.Mac;

// ObjC IMKInputController サブクラス(極薄 native)から呼ぶ C ABI 境界。NativeAOT で
// [UnmanagedCallersOnly] エクスポート。変換ロジック・IPC は C#(ImkController→ImeClient)。
// 文字列は「呼出側バッファに UTF-8 を書き必要長を返す」contract。
public static class ImkBridge
{
    private static ImkController? _controller;
    private static string _preedit = string.Empty;
    private static string _commit = string.Empty;
    private static string _candidates = string.Empty; // 改行区切りの候補列。

    // mozc_server の既定 IPC 名(Mozc.Server.Host の --pipe 既定と一致)。
    private const string DefaultServerName = "mozc.session";

    public static void InitForTest(Func<byte[], byte[]> transport)
        => _controller = new ImkController(transport);

    // native: int mozc_imk_init() -> 1=成功/0=失敗。IMK バンドル起動時に呼び、
    // mozc_server への file socket トランスポートで controller を初期化する。
    [UnmanagedCallersOnly(EntryPoint = "mozc_imk_init")]
    public static int Init()
    {
        try
        {
            EnsureController();
            return _controller != null ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    // controller 未初期化なら実トランスポートで遅延初期化する(init 呼び忘れの保険)。
    private static void EnsureController()
    {
        if (_controller != null || !global::System.OperatingSystem.IsMacOS())
        {
            return;
        }
        _controller = new ImkController(BuildServerTransport(DefaultServerName));
    }

    // mozc_server へ connect-per-call する file socket トランスポート(macOS は
    // abstract socket 非対応のためファイルシステム UDS を使う)。
    [global::System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static Func<byte[], byte[]> BuildServerTransport(string name)
        => request =>
        {
            var pm = new Mozc.Ipc.IpcPathManager();
            if (!pm.TryLoad(name))
            {
                throw new Mozc.Ipc.IpcException($"cannot load .ipc metadata for '{name}'");
            }
            using var client = new Mozc.Ipc.FileSocketIpcClient(pm.GetFileSocketPath());
            return client.Call(request, TimeSpan.FromSeconds(30));
        };

    // native: int mozc_imk_process_key(ushort keyCode, const char* charsUtf8, int charsLen, uint modifiers)
    [UnmanagedCallersOnly(EntryPoint = "mozc_imk_process_key")]
    public static unsafe int ProcessKey(ushort keyCode, byte* charsUtf8, int charsLen, uint modifiers)
    {
        string chars = charsLen > 0 && charsUtf8 != null
            ? Encoding.UTF8.GetString(charsUtf8, charsLen)
            : string.Empty;
        return ProcessKeyCore(keyCode, chars, modifiers);
    }

    // [UnmanagedCallersOnly] 境界を越えた例外は native でプロセスを落とすため全捕捉する。
    private static int ProcessKeyCore(ushort keyCode, string chars, uint modifiers)
    {
        try
        {
            if (_controller == null)
            {
                EnsureController();
                if (_controller == null)
                {
                    return 0;
                }
            }
            Pb.KeyEvent ke = MacKeyTranslator.Translate(keyCode, chars, modifiers);
            Client.ImeState s = ke.HasSpecialKey
                ? _controller.HandleSpecialKey(ke.SpecialKey)
                : _controller.HandleCharacter((char)ke.KeyCode);
            _preedit = s.Preedit;
            _commit = s.Commit;
            _candidates = string.Join('\n', s.Candidates);
            return s.Consumed ? 1 : 0;
        }
        catch
        {
            _controller = null;
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mozc_imk_get_preedit")]
    public static unsafe int GetPreedit(byte* buf, int cap) => WriteUtf8(_preedit, buf, cap);

    [UnmanagedCallersOnly(EntryPoint = "mozc_imk_get_commit")]
    public static unsafe int GetCommit(byte* buf, int cap) => WriteUtf8(_commit, buf, cap);

    [UnmanagedCallersOnly(EntryPoint = "mozc_imk_get_candidates")]
    public static unsafe int GetCandidates(byte* buf, int cap) => WriteUtf8(_candidates, buf, cap);

    public static unsafe int WriteUtf8(string s, byte* buf, int cap)
    {
        int needed = Encoding.UTF8.GetByteCount(s);
        if (buf == null || cap < needed)
        {
            return needed;
        }
        return Encoding.UTF8.GetBytes(s, new Span<byte>(buf, cap));
    }

    // テスト用 managed ラッパ。
    public static (string Preedit, string Commit, bool Consumed) ProcessKeyManaged(
        ushort keyCode, string chars, uint modifiers)
    {
        int c = ProcessKeyCore(keyCode, chars, modifiers);
        return (_preedit, _commit, c != 0);
    }

    // テスト用: 直近の候補列(改行区切り)。
    public static string CandidatesManaged => _candidates;
}
