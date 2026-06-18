#!/bin/sh
# Windows 配布(MSI)。NativeAOT publish 出力(TIP DLL/mozc_server/mozc_tool/mozc.data)を
# WiX v4 で MSI 化(実機 Windows + dotnet tool restore で wix)。
set -e
PUBLISH="${1:-publish}"
wix build Mozc.wxs -d PublishDir="$PUBLISH" -o Mozc.msi
# インストーラのカスタムアクションで TIP DLL を regsvr32 相当登録(DllRegisterServer)。
