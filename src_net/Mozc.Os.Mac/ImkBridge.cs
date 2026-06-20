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

    // mozc_server 未起動時に引数なし spawn して .ipc 公開を待つ(C++ ServerLauncher 相当)。
    // .app バンドルでは server(mozc_server)は IMK バイナリと同じ Contents/MacOS に同梱されるため
    // 既定パス探索(実行ファイルディレクトリ)で足りる。
    private static readonly Mozc.Ipc.ServerLauncher _launcher = new(DefaultServerName);

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
            // .ipc 未ロード、または parseable でも広告 PID が死んでいる(クラッシュ/再起動で残った
            // stale .ipc)場合は server を spawn して再ロードする(死んだソケットへの誤接続を防ぐ)。
            if (!pm.TryLoad(name) || !Mozc.Ipc.ServerLauncher.IsAdvertisedServerAlive(pm))
            {
                // 一度起動できなければ FATAL ラッチで再試行しない(respawn storm 防止)。
                if (!_launcher.EnsureServerRunning() || !pm.TryLoad(name))
                {
                    throw new Mozc.Ipc.IpcException($"cannot load .ipc metadata for '{name}'");
                }
            }
            // サーバの protocol_version が一致しないなら接続しない(ワイヤー非互換の誤接続防止)。
            if (!pm.IsCompatibleProtocolVersion())
            {
                throw new Mozc.Ipc.IpcException(
                    $"incompatible IPC protocol version (server={pm.ServerProtocolVersion})");
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
            // Command(⌘)修飾のキー(Cmd+C/Cmd+V 等)は macOS アプリのショートカットであり IME の
            // 対象外。サーバへ送ると修飾無しの印字キーとして合成/消費されショートカットを横取り
            // するため、未消費(0)を返してアプリへ委ねる(C++ KeyCodeMap が Command で return NO
            // する挙動に一致)。進行中の preedit(marked text)は保持し、確定済み文字列と候補だけ
            // 消す(残すと native が直後の get_commit で前回確定を二重挿入するため)。
            if (MacKeyTranslator.HasCommand(modifiers))
            {
                _commit = string.Empty;
                _candidates = string.Empty;
                return 0;
            }
            if (_controller == null)
            {
                EnsureController();
                if (_controller == null)
                {
                    return 0;
                }
            }
            Pb.KeyEvent ke = MacKeyTranslator.Translate(keyCode, chars, modifiers);
            // 修飾キー(Ctrl/Shift/Alt)込みの完全なイベントを送る。Shift+Space 変換や
            // Ctrl+h backspace 等のショートカットが正しく届くようにする。
            Client.ImeState s = _controller.HandleKeyEvent(ke);
            _preedit = s.Preedit;
            _commit = s.Commit;
            _candidates = string.Join('\n', s.Candidates);
            return s.Consumed ? 1 : 0;
        }
        catch
        {
            _controller = null;
            // キャッシュ済み commit/preedit/candidates を消す。残すと native が直後に
            // mozc_imk_get_commit を読み、前回の確定文字列を二重挿入する恐れがある(ibus 側と同様)。
            _commit = string.Empty;
            _preedit = string.Empty;
            _candidates = string.Empty;
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
