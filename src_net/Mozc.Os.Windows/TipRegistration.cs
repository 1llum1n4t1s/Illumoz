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
        // TODO(実機): ITfInputProcessorProfiles::Register(Clsid) +
        //   ITfInputProcessorProfiles::AddLanguageProfile(Clsid, LangIdJaJp, ProfileGuid, "Mozc", ...) +
        //   ITfCategoryMgr で TIP カテゴリ(ITF_CATEGORY_TIP_KEYBOARD 等)を登録。
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

    private static string GetModulePath()
    {
        // NativeAOT 共有ライブラリ自身のパス。
        return Environment.ProcessPath ?? "Mozc.Os.Windows.dll";
    }
}
