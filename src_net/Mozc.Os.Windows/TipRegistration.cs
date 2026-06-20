using System.Runtime.InteropServices;

namespace Mozc.Os.Windows;

// TSF TIP の COM 自己登録(regsvr32 / インストーラが呼ぶ DllRegisterServer 相当)。
// CLSID の InprocServer32 をレジストリに書き、TSF プロファイル登録(ITfInputProcessorProfiles)
// へ繋ぐ。ここでは CLSID レジストリ登録の「実行計画」を作る部分を分離し dry-run 検証可能にする。
public static class TipRegistration
{
    public static readonly Guid Clsid = new("10a67bc8-22fa-4a59-90dc-2546652c56bf");
    // TSF 言語プロファイル: 言語 ID(ja-JP=0x0411)とプロファイル GUID。
    public const ushort LangIdJaJp = 0x0411;
    public static readonly Guid ProfileGuid = new("0f9d8e2b-1c34-4a77-9b21-7d6e5a4c3b21");

    public sealed record RegistryWrite(string KeyPath, string? ValueName, string Value);

    // CLSID 登録に必要なレジストリ書き込み計画(HKEY_CLASSES_ROOT 相対)。
    public static IReadOnlyList<RegistryWrite> BuildClsidPlan(string dllPath)
    {
        string clsidKey = $@"CLSID\{{{Clsid.ToString().ToUpperInvariant()}}}";
        return new[]
        {
            new RegistryWrite(clsidKey, null, "Mozc (.NET) Text Service"),
            new RegistryWrite($@"{clsidKey}\InprocServer32", null, dllPath),
            new RegistryWrite($@"{clsidKey}\InprocServer32", "ThreadingModel", "Apartment"),
        };
    }

    // 実レジストリへ適用(Windows のみ・要管理者)。テストは BuildClsidPlan を対象にする。
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void ApplyClsidRegistration(string dllPath)
    {
        foreach (RegistryWrite w in BuildClsidPlan(dllPath))
        {
            using Microsoft.Win32.RegistryKey key =
                Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(w.KeyPath);
            key.SetValue(w.ValueName ?? string.Empty, w.Value);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    private const uint CLSCTX_INPROC_SERVER = 0x1;

    // TSF プロファイル/カテゴリ登録(実 TSF を CoCreate して呼ぶ。実機 Windows のみ動作)。
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static int RegisterProfiles(string description = "Mozc")
    {
        // ITfInputProcessorProfiles::Register + AddLanguageProfile。
        Guid iidProfiles = typeof(ITfInputProcessorProfiles).GUID;
        int hr = CoCreateInstance(in TsfGuids.CLSID_TF_InputProcessorProfiles, 0,
            CLSCTX_INPROC_SERVER, in iidProfiles, out nint pProfiles);
        if (hr < 0)
        {
            return hr;
        }
        try
        {
            var profiles = (ITfInputProcessorProfiles)ComInterop.Wrappers
                .GetOrCreateObjectForComInstance(pProfiles, CreateObjectFlags.None);
            hr = profiles.Register(in Clsid);
            if (hr < 0)
            {
                return hr;
            }
            hr = profiles.AddLanguageProfile(in Clsid, LangIdJaJp, in ProfileGuid,
                description, (uint)description.Length, string.Empty, 0, 0);
            if (hr < 0)
            {
                return hr;
            }
        }
        finally
        {
            Marshal.Release(pProfiles);
        }

        // ITfCategoryMgr::RegisterCategory(TIP_KEYBOARD / DISPLAYATTRIBUTEPROVIDER)。
        Guid iidCat = typeof(ITfCategoryMgr).GUID;
        hr = CoCreateInstance(in TsfGuids.CLSID_TF_CategoryMgr, 0,
            CLSCTX_INPROC_SERVER, in iidCat, out nint pCat);
        if (hr < 0)
        {
            return hr;
        }
        try
        {
            var cat = (ITfCategoryMgr)ComInterop.Wrappers
                .GetOrCreateObjectForComInstance(pCat, CreateObjectFlags.None);
            hr = cat.RegisterCategory(in Clsid, in TsfGuids.GUID_TFCAT_TIP_KEYBOARD, in Clsid);
            if (hr < 0)
            {
                return hr;
            }
            hr = cat.RegisterCategory(in Clsid, in TsfGuids.GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER, in Clsid);
            if (hr < 0)
            {
                return hr;
            }
        }
        finally
        {
            Marshal.Release(pCat);
        }
        return 0;
    }

    // TSF プロファイル/カテゴリ登録の解除(アンインストール/アップグレード時)。
    // CLSID レジストリ削除だけだと、TSF 側に存在しない CLSID を指す入力プロファイルが残り、
    // 古い Mozc エントリやアクティベーション失敗の原因になる。CLSID 削除前に呼ぶ。
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static int UnregisterProfiles()
    {
        // カテゴリ登録を解除(Register と逆順)。
        Guid iidCat = typeof(ITfCategoryMgr).GUID;
        if (CoCreateInstance(in TsfGuids.CLSID_TF_CategoryMgr, 0,
                CLSCTX_INPROC_SERVER, in iidCat, out nint pCat) >= 0)
        {
            try
            {
                var cat = (ITfCategoryMgr)ComInterop.Wrappers
                    .GetOrCreateObjectForComInstance(pCat, CreateObjectFlags.None);
                cat.UnregisterCategory(in Clsid, in TsfGuids.GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER, in Clsid);
                cat.UnregisterCategory(in Clsid, in TsfGuids.GUID_TFCAT_TIP_KEYBOARD, in Clsid);
            }
            finally
            {
                Marshal.Release(pCat);
            }
        }

        // プロファイル/言語プロファイルを解除(Unregister は AddLanguageProfile 分も含めて除去する)。
        Guid iidProfiles = typeof(ITfInputProcessorProfiles).GUID;
        if (CoCreateInstance(in TsfGuids.CLSID_TF_InputProcessorProfiles, 0,
                CLSCTX_INPROC_SERVER, in iidProfiles, out nint pProfiles) >= 0)
        {
            try
            {
                var profiles = (ITfInputProcessorProfiles)ComInterop.Wrappers
                    .GetOrCreateObjectForComInstance(pProfiles, CreateObjectFlags.None);
                profiles.Unregister(in Clsid);
            }
            finally
            {
                Marshal.Release(pProfiles);
            }
        }
        return 0;
    }

    // regsvr32 がロードした DLL に対し呼ぶ自己登録エクスポート。
    [UnmanagedCallersOnly(EntryPoint = "DllRegisterServer")]
    public static int DllRegisterServer()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string dll = GetModulePath();
                ApplyClsidRegistration(dll);
                // CLSID 登録後に TSF プロファイル/カテゴリ登録。失敗 HRESULT は installer(regsvr32/MSI)へ
                // 伝播する。破棄して S_OK を返すと、CLSID だけ書けて TSF プロファイル登録が失敗しても
                // インストールが成功扱いになり、Windows が Mozc を一覧/有効化できなくなる。
                int hr = RegisterProfiles();
                if (hr < 0)
                {
                    return hr;
                }
            }
            return 0; // S_OK
        }
        catch (Exception)
        {
            return unchecked((int)0x80004005); // E_FAIL
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DllUnregisterServer")]
    public static int DllUnregisterServer()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // CLSID レジストリ削除より先に TSF プロファイル/カテゴリ登録を解除する
                // (TSF が無い環境では失敗しても続行)。
                try { UnregisterProfiles(); } catch { /* TSF 未導入環境は無視 */ }
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(
                    $@"CLSID\{{{Clsid.ToString().ToUpperInvariant()}}}", throwOnMissingSubKey: false);
            }
            return 0;
        }
        catch (Exception)
        {
            return unchecked((int)0x80004005);
        }
    }

    private const uint GetModuleHandleExFlagFromAddress = 0x4;
    private const uint GetModuleHandleExFlagUnchangedRefcount = 0x2;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetModuleHandleExW(uint flags, IntPtr address, out IntPtr module);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(IntPtr module, [Out] char[] buffer, uint size);

    // regsvr32/インストーラのカスタムアクションから DllRegisterServer が呼ばれると
    // Environment.ProcessPath は host プロセス(regsvr32 等)を指すため、InprocServer32 に
    // 誤ったパスを書いてしまう。ロード中の TIP DLL 自身のモジュールパスを解決する。
    private static unsafe string GetModulePath()
    {
        IntPtr addr = (IntPtr)(delegate* unmanaged<int>)&ComExports.DllCanUnloadNow;
        if (GetModuleHandleExW(
                GetModuleHandleExFlagFromAddress | GetModuleHandleExFlagUnchangedRefcount,
                addr, out IntPtr module) && module != IntPtr.Zero)
        {
            var buf = new char[1024];
            uint len = GetModuleFileNameW(module, buf, (uint)buf.Length);
            if (len > 0 && len < buf.Length)
            {
                return new string(buf, 0, (int)len);
            }
        }
        return Environment.ProcessPath ?? "Mozc.Os.Windows.dll";
    }
}
