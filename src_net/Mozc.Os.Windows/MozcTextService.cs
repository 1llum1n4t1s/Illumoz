using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Mozc.Os.Windows;

// C++ src/win32/tip/tip_text_service.cc 相当。TSF が CoCreate する TIP 本体の COM クラス。
// [GeneratedComClass] により NativeAOT で COM 公開可能(vtable はコンパイル時生成)。
// Activate でスレッドマネージャ/キーイベントシンクを登録し、本体ロジックは TipController(C#)。
// ここではアクティブ化ライフサイクルの骨格(キーシンク advise / composition 反映は実機結線時)。
[GeneratedComClass]
[Guid("10a67bc8-22fa-4a59-90dc-2546652c56bf")] // Mozc TIP CLSID(C++ tsf_profile.h と整合させる)
public partial class MozcTextService : ITfTextInputProcessorEx
{
    private const int S_OK = 0;

    // mozc_server の既定 IPC 名(Mozc.Server.Host の --pipe 既定と一致)。
    private const string DefaultServerName = "mozc.session";

    private nint _threadMgr;
    private uint _clientId;
    private TipController? _controller;

    // テスト/未結線時に差し替え可能。null なら Activate で既定 NamedPipe を生成する。
    public Func<byte[], byte[]>? Transport { get; set; }

    // 生存中はモジュール参照カウントを上げ、DllCanUnloadNow が S_FALSE を返すようにする。
    public MozcTextService() => ComExports.AddRef();

    ~MozcTextService() => ComExports.Release();

    // mozc_server へ connect-per-call する NamedPipe トランスポート。
    private static Func<byte[], byte[]> BuildServerTransport(string name)
        => request =>
        {
            var pm = new Mozc.Ipc.IpcPathManager();
            if (!pm.TryLoad(name))
            {
                throw new Mozc.Ipc.IpcException($"cannot load .ipc metadata for '{name}'");
            }
            using var client = new Mozc.Ipc.NamedPipeIpcClient(pm.GetWindowsPipeName());
            return client.Call(request, global::System.TimeSpan.FromSeconds(30));
        };

    public int Activate(nint threadMgr, uint clientId)
    {
        _threadMgr = threadMgr;
        _clientId = clientId;
        // テスト未注入時は既定の NamedPipe トランスポートを生成する
        // (COM 生成された実機 TIP が transport 無しで controller を持てない不具合の修正)。
        Func<byte[], byte[]> transport = Transport ?? BuildServerTransport(DefaultServerName);
        _controller = new TipController(transport);
        _controller.EnsureSession();
        // TODO(実機): ITfThreadMgr から ITfKeyEventSink を AdviseSink、表示属性登録等。
        return S_OK;
    }

    public int ActivateEx(nint threadMgr, uint clientId, uint flags)
        => Activate(threadMgr, clientId);

    public int Deactivate()
    {
        _controller?.Shutdown();
        _controller = null;
        _threadMgr = 0;
        _clientId = 0;
        // TODO(実機): UnadviseSink 等のクリーンアップ。
        return S_OK;
    }

    // 実機のキーイベントシンクから呼ぶ想定(現状はロジック確認用の薄い橋)。
    public bool OnTestKeyDown(char c) => _controller?.OnCharacter(c) ?? false;
}
