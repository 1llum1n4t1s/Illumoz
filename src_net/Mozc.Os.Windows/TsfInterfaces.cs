using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Mozc.Os.Windows;

// C++ TSF(msctf.h)の最小インターフェース定義(NativeAOT COM: source generator が
// vtable/marshalling を生成し、シグネチャをコンパイル時検証する)。
// ITfThreadMgr 等の複合型は当面 IntPtr(opaque) で受ける。実フル定義は実機結線時に拡張。

// ITfTextInputProcessor: TIP のアクティブ化入口。
// IID: aa80e7f7-2021-11d2-93e0-0060b067b86e
[GeneratedComInterface]
[Guid("aa80e7f7-2021-11d2-93e0-0060b067b86e")]
public partial interface ITfTextInputProcessor
{
    // Activate(ITfThreadMgr *ptim, TfClientId tid)
    [PreserveSig] int Activate(nint threadMgr, uint clientId);

    // Deactivate()
    [PreserveSig] int Deactivate();
}

// ITfTextInputProcessorEx: Activate に追加フラグ。
// IID: 6e4e2102-f9cd-433a-b760-d419f78f5360
[GeneratedComInterface]
[Guid("6e4e2102-f9cd-433a-b760-d419f78f5360")]
public partial interface ITfTextInputProcessorEx : ITfTextInputProcessor
{
    // ActivateEx(ITfThreadMgr *ptim, TfClientId tid, DWORD dwFlags)
    [PreserveSig] int ActivateEx(nint threadMgr, uint clientId, uint flags);
}
