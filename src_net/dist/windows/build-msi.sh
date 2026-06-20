#!/bin/sh
# Windows 配布(MSI)。NativeAOT publish 出力(TIP DLL/mozc_server/mozc_tool/mozc.data)を
# WiX v4 で MSI 化(実機 Windows + dotnet tool restore で wix)。
set -e
PUBLISH="${1:-publish}"
# Mozc.wxs は $(PublishDir)\mozc.data / roman.tsv / keymap.tsv を参照する。NativeAOT publish は
# これらを出力しないため、Linux/mac パッケージと同様に wix build の前に PUBLISH へ stage する
# (欠けると wix build が missing source file で失敗する)。
# mozc.data は GenerateMozcData ターゲットが Mozc.Server.Host 直下に生成する(MOZC_DATA で上書き可)。
MOZC_DATA="${MOZC_DATA:-../../Mozc.Server.Host/mozc.data}"
cp "$MOZC_DATA" "$PUBLISH/mozc.data"
cp ../../../src/data/preedit/romanji-hiragana.tsv "$PUBLISH/roman.tsv"
cp ../../../src/data/keymap/ms-ime.tsv "$PUBLISH/keymap.tsv"
wix build Mozc.wxs -d PublishDir="$PUBLISH" -o Mozc.msi
# インストーラのカスタムアクションで TIP DLL を regsvr32 相当登録(DllRegisterServer)。
