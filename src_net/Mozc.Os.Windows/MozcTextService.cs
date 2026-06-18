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

    private nint _threadMgr;
    private uint _clientId;
    private TipController? _controller;

    // 実機ではここで NamedPipe client を transport にする。テスト/未結線時は差し替え可能。
    public Func<byte[], byte[]>? Transport { get; set; }

    public int Activate(nint threadMgr, uint clientId)
    {
        _threadMgr = threadMgr;
        _clientId = clientId;
        if (Transport != null)
        {
            _controller = new TipController(Transport);
            _controller.EnsureSession();
        }
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
