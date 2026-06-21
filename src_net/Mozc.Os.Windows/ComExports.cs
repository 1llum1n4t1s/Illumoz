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

    // Mozc TIP の CLSID(tsf_profile.h と整合させる)。
    private static readonly Guid MozcTipClsid = new("10a67bc8-22fa-4a59-90dc-2546652c56bf");

    // DllGetClassObject: COM がクラスファクトリを要求する入口。
    // 要求 CLSID が Mozc TIP なら MozcClassFactory を生成し riid に QI して返す。
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, void** ppv)
    {
        if (ppv != null)
        {
            *ppv = null;
        }
        if (rclsid == null || riid == null || ppv == null)
        {
            return unchecked((int)0x80070057); // E_INVALIDARG
        }
        if (*rclsid != MozcTipClsid)
        {
            return CLASS_E_CLASSNOTAVAILABLE;
        }

        var factory = new MozcClassFactory();
        nint unknown = ComInterop.Wrappers.GetOrCreateComInterfaceForObject(
            factory, CreateComInterfaceFlags.None);
        try
        {
            Guid iid = *riid;
            int hr = System.Runtime.InteropServices.Marshal.QueryInterface(unknown, in iid, out nint result);
            if (hr == S_OK)
            {
                *ppv = (void*)result;
            }
            return hr;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.Release(unknown);
        }
    }

    // 生存中の COM オブジェクト数 + LockServer ロック数。0 になるまで DLL を
    // アンロードさせない(C++ src/win32/tip の TipDllModule 参照カウント相当)。
    private static int _objectCount;

    internal static void AddRef() => System.Threading.Interlocked.Increment(ref _objectCount);

    internal static void Release() => System.Threading.Interlocked.Decrement(ref _objectCount);

    // DllCanUnloadNow: 生存オブジェクトが残っていればアンロード不可(S_FALSE)。
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow()
        => System.Threading.Volatile.Read(ref _objectCount) == 0 ? S_OK : S_FALSE;
}
