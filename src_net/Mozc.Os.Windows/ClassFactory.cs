using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Mozc.Os.Windows;

// IClassFactory(COM 標準)。DllGetClassObject が返し、TSF が CreateInstance で TIP を生成する。
// IID: 00000001-0000-0000-C000-000000000046
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
public partial interface IClassFactory
{
    [PreserveSig] int CreateInstance(nint outer, in Guid riid, out nint ppv);
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

// Mozc TIP の class factory。CreateInstance で MozcTextService を生成し riid に QI して返す。
[GeneratedComClass]
public partial class MozcClassFactory : IClassFactory
{
    private const int S_OK = 0;
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);

    // TIP 実体を生成する factory(transport 注入可)。
    public Func<MozcTextService>? ServiceFactory { get; set; }

    // factory 自身も COM オブジェクトとして生存数に数え、保持中は DLL アンロードを防ぐ
    // (C++ TipClassFactory のコンストラクタ/デストラクタで g_module_ref を増減するのと同じ)。
    public MozcClassFactory() => ComExports.AddRef();

    ~MozcClassFactory() => ComExports.Release();

    public int CreateInstance(nint outer, in Guid riid, out nint ppv)
    {
        ppv = 0;
        if (outer != 0)
        {
            return CLASS_E_NOAGGREGATION; // アグリゲーション非対応
        }

        MozcTextService service = ServiceFactory?.Invoke() ?? new MozcTextService();
        nint unknown = ComInterop.Wrappers.GetOrCreateComInterfaceForObject(
            service, CreateComInterfaceFlags.None);
        try
        {
            Guid iid = riid;
            int hr = Marshal.QueryInterface(unknown, in iid, out ppv);
            return hr == S_OK ? S_OK : E_NOINTERFACE;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    // LockServer はモジュールをメモリに保持する。ロック中は DllCanUnloadNow を S_FALSE にする。
    public int LockServer(bool fLock)
    {
        if (fLock)
        {
            ComExports.AddRef();
        }
        else
        {
            ComExports.Release();
        }
        return S_OK;
    }
}

// NativeAOT COM の ComWrappers 戦略(GeneratedComInterface/Class 用)。
internal static class ComInterop
{
    public static readonly StrategyBasedComWrappers Wrappers = new();
}
