using System.Runtime.InteropServices;

namespace Mozc.Os.Windows;

// C++ src/win32/tip/mozc_tip.def の DLL エクスポート相当(極薄 native 境界)。
// NativeAOT で in-proc COM サーバ(TSF TIP DLL)を出すための [UnmanagedCallersOnly] エクスポート。
// 実体の class factory / ITfTextInputProcessor 実装(GeneratedComInterface/GeneratedComClass)と
// CLSID 登録(ITfInputProcessorProfiles::Register)は実機 Windows + AOT publish で結線する。
// 現状はエクスポート面の骨格(コンパイル可能)。
internal static class ComExports
{
    // HRESULT 定数。
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int CLASS_E_CLASSNOTAVAILABLE = unchecked((int)0x80040111);

    // DllGetClassObject: COM がクラスファクトリを要求する入口。
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, void** ppv)
    {
        if (ppv != null)
        {
            *ppv = null;
        }
        // TODO(実機): 自前 IClassFactory(GeneratedComClass)を生成し ppv に返す。
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    // DllCanUnloadNow: ロード中オブジェクトが無ければ S_OK。
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow()
    {
        // TODO(実機): アクティブな COM オブジェクト数を見て判定。骨格では常にアンロード可。
        return S_OK;
    }
}
