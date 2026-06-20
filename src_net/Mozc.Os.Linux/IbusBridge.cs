using System.Runtime.InteropServices;
using System.Text;
using Pb = Mozc.Commands;

namespace Mozc.Os.Linux;

// ibus の native GObject stub(C)から呼ばれる C ABI 境界。NativeAOT で
// [UnmanagedCallersOnly] エクスポートし、native 側 process-key-event handler が叩く。
// 文字列は「呼び出し側が用意したバッファに UTF-8 を書き、必要長を返す」標準 C ABI で受け渡す。
// 本体ロジックは IbusEngineController(=ImeClient 共有)。
// クラス全体を SupportedOSPlatform("linux") にするとクロスプラットフォームな
// テスト/エクスポート呼び出しが CA1416 になるため、Linux 限定 API を使う
// BuildServerTransport にのみ属性を付け、EnsureController で IsLinux ガードする。
public static class IbusBridge
{
    private static IbusEngineController? _controller;
    private static string _preedit = string.Empty;
    private static string _commit = string.Empty;
    private static string _candidates = string.Empty; // 改行区切りの候補列。
    private static int _focusedIndex = -1; // 注目候補(lookup table のカーソル。-1=未注目)。

    // mozc_server の既定 IPC 名(Mozc.Server.Host の --pipe 既定と一致)。
    private const string DefaultServerName = "mozc.session";

    // テスト/結線用: transport を差し替えて初期化(実機は NamedPipe/Unix client)。
    public static void InitForTest(Func<byte[], byte[]> transport)
        => _controller = new IbusEngineController(transport);

    // native: int mozc_ibus_init() -> 1=成功/0=失敗。
    // 実機の ibus-engine-mozc 起動時(main)に呼ばれ、mozc_server への
    // Unix abstract socket トランスポートで controller を初期化する。
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_init")]
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
        if (_controller != null || !global::System.OperatingSystem.IsLinux())
        {
            return;
        }
        _controller = new IbusEngineController(BuildServerTransport(DefaultServerName));
    }

    // mozc_server へ connect-per-call する Unix abstract socket トランスポート。
    [global::System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static Func<byte[], byte[]> BuildServerTransport(string name)
        => request =>
        {
            var pm = new Mozc.Ipc.IpcPathManager();
            if (!pm.TryLoad(name))
            {
                throw new Mozc.Ipc.IpcException($"cannot load .ipc metadata for '{name}'");
            }
            // サーバの protocol_version が一致しないなら接続しない(ワイヤー非互換の誤接続防止)。
            if (!pm.IsCompatibleProtocolVersion())
            {
                throw new Mozc.Ipc.IpcException(
                    $"incompatible IPC protocol version (server={pm.ServerProtocolVersion})");
            }
            using var client = new Mozc.Ipc.UnixSocketIpcClient(pm.GetLinuxAbstractSocketName());
            return client.Call(request, TimeSpan.FromSeconds(30));
        };

    // native: int mozc_ibus_process_key(uint keyval, uint state) -> consumed(0/1)
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_process_key")]
    public static int ProcessKey(uint keyval, uint state) => ProcessKeyCore(keyval, state);

    // managed 実体(UnmanagedCallersOnly からも managed テストからも呼べる)。
    // [UnmanagedCallersOnly] 境界を越えて例外が native に伝播するとプロセスが落ちるため、
    // IPC 例外を含む全例外を捕捉し、controller をリセットして 0(未消費)を返す。
    private static int ProcessKeyCore(uint keyval, uint state)
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
            Pb.KeyEvent ke = X11Keysym.Translate(keyval, state);
            ImeStateForBridge st = ProcessInternal(ke);
            _preedit = st.Preedit;
            _commit = st.Commit;
            _candidates = st.Candidates;
            _focusedIndex = st.FocusedIndex;
            return st.Consumed ? 1 : 0;
        }
        catch
        {
            _controller = null; // 壊れた接続は破棄し次回再初期化させる。
            // キャッシュ済みの commit/preedit/candidates を消す。残すと native が直後に
            // mozc_ibus_get_commit を読み、前回の確定文字列を二重挿入する恐れがある。
            _commit = string.Empty;
            _preedit = string.Empty;
            _candidates = string.Empty;
            _focusedIndex = -1;
            return 0;
        }
    }

    private readonly record struct ImeStateForBridge(
        string Preedit, string Commit, string Candidates, bool Consumed, int FocusedIndex);

    private static ImeStateForBridge ProcessInternal(Pb.KeyEvent ke)
    {
        // 修飾キー(Ctrl/Shift/Alt)込みの完全なイベントを送る。Shift+Space 変換や
        // Ctrl+h backspace 等のショートカットが正しく届くようにする。
        Client.ImeState s = _controller!.ProcessKeyEvent(ke);
        return new ImeStateForBridge(
            s.Preedit, s.Commit, string.Join('\n', s.Candidates), s.Consumed, s.FocusedIndex);
    }

    // native: int mozc_ibus_get_focused_index() -> 注目候補の行(lookup table のカーソル位置)。
    // -1 は未注目(サジェスト等)。native は >=0 のとき ibus_lookup_table_set_cursor_pos する。
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_get_focused_index")]
    public static int GetFocusedIndex() => _focusedIndex;

    // native: int mozc_ibus_get_preedit(byte* buf, int cap) -> 書込みバイト数(cap 不足時は必要長)
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_get_preedit")]
    public static unsafe int GetPreedit(byte* buf, int cap) => WriteUtf8(_preedit, buf, cap);

    // native: int mozc_ibus_get_commit(byte* buf, int cap)
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_get_commit")]
    public static unsafe int GetCommit(byte* buf, int cap) => WriteUtf8(_commit, buf, cap);

    // native: int mozc_ibus_get_candidates(byte* buf, int cap) -> 改行区切り候補列。
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_get_candidates")]
    public static unsafe int GetCandidates(byte* buf, int cap) => WriteUtf8(_candidates, buf, cap);

    // 呼び出し側バッファに UTF-8 を書く。cap 不足なら書かずに必要長を返す。共通ヘルパ(テスト可能)。
    public static unsafe int WriteUtf8(string s, byte* buf, int cap)
    {
        int needed = Encoding.UTF8.GetByteCount(s);
        if (buf == null || cap < needed)
        {
            return needed;
        }
        var span = new Span<byte>(buf, cap);
        return Encoding.UTF8.GetBytes(s, span);
    }

    // テスト用の純 managed ラッパ(直近の preedit/commit を返す)。
    public static (string Preedit, string Commit, bool Consumed) ProcessKeyManaged(uint keyval, uint state)
    {
        int consumed = ProcessKeyCore(keyval, state);
        return (_preedit, _commit, consumed != 0);
    }

    // テスト用: 直近の候補列(改行区切り)。
    public static string CandidatesManaged => _candidates;
}
