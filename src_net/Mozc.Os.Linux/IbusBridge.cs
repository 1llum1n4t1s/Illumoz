using System.Runtime.InteropServices;
using System.Text;
using Pb = Mozc.Commands;

namespace Mozc.Os.Linux;

// ibus の native GObject stub(C)から呼ばれる C ABI 境界。NativeAOT で
// [UnmanagedCallersOnly] エクスポートし、native 側 process-key-event handler が叩く。
// 文字列は「呼び出し側が用意したバッファに UTF-8 を書き、必要長を返す」標準 C ABI で受け渡す。
// 本体ロジックは IbusEngineController(=ImeClient 共有)。
public static class IbusBridge
{
    private static IbusEngineController? _controller;
    private static string _preedit = string.Empty;
    private static string _commit = string.Empty;
    private static string _candidates = string.Empty; // 改行区切りの候補列。

    // テスト/結線用: transport を差し替えて初期化(実機は NamedPipe/Unix client)。
    public static void InitForTest(Func<byte[], byte[]> transport)
        => _controller = new IbusEngineController(transport);

    // native: int mozc_ibus_process_key(uint keyval, uint state) -> consumed(0/1)
    [UnmanagedCallersOnly(EntryPoint = "mozc_ibus_process_key")]
    public static int ProcessKey(uint keyval, uint state) => ProcessKeyCore(keyval, state);

    // managed 実体(UnmanagedCallersOnly からも managed テストからも呼べる)。
    private static int ProcessKeyCore(uint keyval, uint state)
    {
        if (_controller == null)
        {
            return 0;
        }
        Pb.KeyEvent ke = X11Keysym.Translate(keyval, state);
        ImeStateForBridge st = ProcessInternal(ke);
        _preedit = st.Preedit;
        _commit = st.Commit;
        _candidates = st.Candidates;
        return st.Consumed ? 1 : 0;
    }

    private readonly record struct ImeStateForBridge(
        string Preedit, string Commit, string Candidates, bool Consumed);

    private static ImeStateForBridge ProcessInternal(Pb.KeyEvent ke)
    {
        Client.ImeState s = ke.HasSpecialKey
            ? _controller!.ProcessSpecial(ke.SpecialKey)
            : _controller!.ProcessCharacter((char)ke.KeyCode);
        return new ImeStateForBridge(
            s.Preedit, s.Commit, string.Join('\n', s.Candidates), s.Consumed);
    }

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
