using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Mozc.Os.Windows;

// TSF プロファイル/カテゴリ登録に使う COM インターフェース(msctf.h)。
// vtable スロット順を保つため呼び出すメソッドまでを宣言順どおり定義する。

// ITfInputProcessorProfiles。IID: 1f02b6c5-7842-4ee6-8a0b-9a24183a95ca
// vtable: 1=Register, 2=Unregister, 3=AddLanguageProfile（以降は未使用なので省略）。
[GeneratedComInterface]
[Guid("1f02b6c5-7842-4ee6-8a0b-9a24183a95ca")]
public partial interface ITfInputProcessorProfiles
{
    [PreserveSig] int Register(in Guid rclsid);
    [PreserveSig] int Unregister(in Guid rclsid);
    [PreserveSig] int AddLanguageProfile(
        in Guid rclsid, ushort langid, in Guid guidProfile,
        [MarshalAs(UnmanagedType.LPWStr)] string description, uint cchDesc,
        [MarshalAs(UnmanagedType.LPWStr)] string iconFile, uint cchFile, uint uIconIndex);
}

// ITfCategoryMgr。IID: c3acefb5-f69d-4905-938f-fcadcf4be830
// vtable: 1=RegisterCategory, 2=UnregisterCategory（以降省略）。
[GeneratedComInterface]
[Guid("c3acefb5-f69d-4905-938f-fcadcf4be830")]
public partial interface ITfCategoryMgr
{
    [PreserveSig] int RegisterCategory(in Guid rclsid, in Guid rcatid, in Guid rguid);
    [PreserveSig] int UnregisterCategory(in Guid rclsid, in Guid rcatid, in Guid rguid);
}

// TSF 既知 GUID(msctf.h / ctffunc.h)。
internal static class TsfGuids
{
    public static readonly Guid CLSID_TF_InputProcessorProfiles =
        new("33c53a50-f456-4884-b049-85fd643ecfed");
    public static readonly Guid CLSID_TF_CategoryMgr =
        new("a4b544a1-438d-4b41-9325-869523e2d6c7");
    // TIP カテゴリ。
    public static readonly Guid GUID_TFCAT_TIP_KEYBOARD =
        new("34745c63-b2f0-4784-8b67-5e12c8701a31");
    public static readonly Guid GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER =
        new("046b8c80-1647-40f7-9b21-b93b81aabc1b");
}
