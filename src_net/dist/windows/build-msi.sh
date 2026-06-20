#!/bin/sh
# Windows 配布(MSI)。NativeAOT publish 出力(TIP DLL/mozc_server/mozc_tool/mozc.data)を
# WiX v4 で MSI 化(実機 Windows + dotnet tool restore で wix)。
set -eu
cd "$(dirname "$0")"
# 同梱元ファイルの存在を事前検証する(欠けた不完全成果物を wix build へ渡さない)。
need() { [ -e "$1" ] || { echo "missing source: $1" >&2; exit 1; }; }
PUBLISH="${1:-publish}"
# Mozc.wxs は $(PublishDir)\mozc.data / roman.tsv / keymap.tsv を参照する。NativeAOT publish は
# これらを出力しないため、Linux/mac パッケージと同様に wix build の前に PUBLISH へ stage する
# (欠けると wix build が missing source file で失敗する)。
# mozc.data は GenerateMozcData ターゲットが Mozc.Server.Host 直下に生成する(MOZC_DATA で上書き可)。
MOZC_DATA="${MOZC_DATA:-../../Mozc.Server.Host/mozc.data}"
need "$MOZC_DATA"
need ../../../src/data/preedit/romanji-hiragana.tsv
need ../../../src/data/preedit/kana.tsv
need ../../../src/data/keymap/ms-ime.tsv
cp "$MOZC_DATA" "$PUBLISH/mozc.data"
cp ../../../src/data/preedit/romanji-hiragana.tsv "$PUBLISH/roman.tsv"
# preedit_method=KANA 用のかな配列(同梱しないとかな入力がローマ字のままになる)。
cp ../../../src/data/preedit/kana.tsv "$PUBLISH/kana.tsv"
cp ../../../src/data/keymap/ms-ime.tsv "$PUBLISH/keymap.tsv"
# session_keymap プリセット(ms-ime/atok/kotoeri/mobile/chromeos)を keymap\ サブディレクトリへ
# stage する(Mozc.wxs の KeymapDir が参照。同梱しないと既定以外へ切替できない)。
mkdir -p "$PUBLISH/keymap"
for f in ms-ime atok kotoeri mobile chromeos; do
  need "../../../src/data/keymap/$f.tsv"
  cp "../../../src/data/keymap/$f.tsv" "$PUBLISH/keymap/$f.tsv"
done
# Mozc.wxs の RegisterTip カスタムアクションは Util 拡張(Wix4UtilCA_X64)を使うため、
# ビルド前に拡張を取得し -ext で参照する(欠けると "unresolved reference" でビルド失敗)。
wix extension add -g WixToolset.Util.wixext/5.0.2
wix build Mozc.wxs -ext WixToolset.Util.wixext -d PublishDir="$PUBLISH" -o Mozc.msi
# インストーラのカスタムアクションで TIP DLL を regsvr32 相当登録(DllRegisterServer)。
