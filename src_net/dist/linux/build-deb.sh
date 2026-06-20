#!/bin/sh
# Linux 配布(deb)。NativeAOT publish 成果物 + ibus stub + component.xml をパッケージ化(実機)。
# set -u: 未定義変数を検出。cd: 呼び出し CWD に依らずスクリプト位置基準で相対パスを解決
# (別ディレクトリから叩いても ../../../src/data 等の cp が静かに失敗しないように)。
set -eu
cd "$(dirname "$0")"
# 同梱元ファイルの存在を事前検証する(欠けた不完全パッケージを dpkg-deb へ渡さない)。
need() { [ -e "$1" ] || { echo "missing source: $1" >&2; exit 1; }; }
STAGE="${1:-stage}"
mkdir -p "$STAGE/usr/lib/ibus-mozc" "$STAGE/usr/lib/mozc" \
         "$STAGE/usr/share/ibus/component" "$STAGE/DEBIAN"
cp ../../Mozc.Server.Host/bin/Release/net10.0/linux-x64/publish/Mozc.Server.Host "$STAGE/usr/lib/mozc/mozc_server"
# ibus エンジンは mozc_server 未起動時に ServerLauncher で引数なし spawn する。Program.cs は
# 引数省略時に実行ファイル(/usr/lib/mozc/mozc_server)と同じディレクトリの同梱データへ
# フォールバックするため、mozc.data/roman.tsv/keymap.tsv をこの配置で同梱する。
# mozc.data は GenerateMozcData ターゲットが Mozc.Server.Host 直下に生成する(MOZC_DATA で上書き可)。
MOZC_DATA="${MOZC_DATA:-../../Mozc.Server.Host/mozc.data}"
need "$MOZC_DATA"
need ../../../src/data/preedit/romanji-hiragana.tsv
need ../../../src/data/keymap/ms-ime.tsv
cp "$MOZC_DATA" "$STAGE/usr/lib/mozc/mozc.data"
cp ../../../src/data/preedit/romanji-hiragana.tsv "$STAGE/usr/lib/mozc/roman.tsv"
cp ../../../src/data/keymap/ms-ime.tsv "$STAGE/usr/lib/mozc/keymap.tsv"
# SET_CONFIG の session_keymap(ATOK/KOTOERI/MOBILE/CHROMEOS/MSIME)プリセットは
# <datadir>/keymap/<preset>.tsv から解決される(KeymapPresets.Load)。プリセット tsv 一式を
# /usr/lib/mozc/keymap/ に同梱しないと、既定以外のキーマップへ切替できない。
mkdir -p "$STAGE/usr/lib/mozc/keymap"
for f in ms-ime atok kotoeri mobile chromeos; do
  cp "../../../src/data/keymap/$f.tsv" "$STAGE/usr/lib/mozc/keymap/$f.tsv"
done
# 設定 GUI(mozc.xml の <setup> が /usr/lib/mozc/mozc_tool を参照する)。同梱しないと
# IBus の「設定」から存在しないファイルを起動してしまう。
cp ../../Mozc.Gui.App/bin/Release/net10.0/linux-x64/publish/Mozc.Gui.App "$STAGE/usr/lib/mozc/mozc_tool"
# ibus-engine-mozc は NativeAOT 共有ライブラリ Mozc.Os.Linux.so にリンクされるため、
# 実行ファイルと .so の両方を同梱する(欠けるとクリーン環境でローダが失敗する)。
cp ../../Mozc.Os.Linux/native/ibus-engine-mozc "$STAGE/usr/lib/ibus-mozc/"
cp ../../Mozc.Os.Linux/bin/Release/net10.0/linux-x64/publish/Mozc.Os.Linux.so "$STAGE/usr/lib/ibus-mozc/"
cp ../ibus/mozc.xml "$STAGE/usr/share/ibus/component/"
cat > "$STAGE/DEBIAN/control" <<CTRL
Package: mozc-dotnet
Version: 1.0.0
Architecture: amd64
Depends: ibus
Maintainer: Mozc .NET migration
Description: Mozc Japanese input (.NET/C#)
CTRL
dpkg-deb --build "$STAGE" mozc-dotnet_1.0.0_amd64.deb
