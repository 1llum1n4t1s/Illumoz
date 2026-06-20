#!/bin/sh
# macOS 配布(pkg)。Mozc.app(IMK bundle)を組み立て pkgbuild で .pkg 化(実機 macOS)。
# set -u: 未定義変数を検出。cd: 呼び出し CWD に依らずスクリプト位置基準で相対パスを解決
# (別ディレクトリから叩いても ../../../src/data 等の cp が静かに失敗しないように)。
set -eu
cd "$(dirname "$0")"
# 同梱元ファイルの存在を事前検証する(欠けた不完全パッケージを pkgbuild へ渡さない)。
need() { [ -e "$1" ] || { echo "missing source: $1" >&2; exit 1; }; }
APP="Mozc.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Info.plist "$APP/Contents/"
# native/build.sh は Mozc.Os.Mac を作業ディレクトリにして `-o Mozc` を出力する
# (= プロジェクトルート直下の Mozc.Os.Mac/Mozc)。native/ ではないので注意。
cp ../../Mozc.Os.Mac/Mozc "$APP/Contents/MacOS/"
# IMK stub は NativeAOT 共有ライブラリ Mozc.Os.Mac.dylib にリンクされるため同梱必須。
cp ../../Mozc.Os.Mac/bin/Release/net10.0/osx-arm64/publish/Mozc.Os.Mac.dylib "$APP/Contents/MacOS/"
cp ../../Mozc.Server.Host/bin/Release/net10.0/osx-arm64/publish/Mozc.Server.Host "$APP/Contents/MacOS/mozc_server"
# IMK は mozc_server 未起動時に ServerLauncher で引数なし spawn する。Program.cs は引数省略時に
# 実行ファイル(Contents/MacOS/mozc_server)の隣の ../Resources の同梱データへフォールバックする
# ため、変換データ・ローマ字表・キーマップを Contents/Resources に同梱する。
MOZC_DATA="${MOZC_DATA:-../../Mozc.Server.Host/mozc.data}"
need "$MOZC_DATA"
need ../../../src/data/preedit/romanji-hiragana.tsv
need ../../../src/data/keymap/ms-ime.tsv
cp "$MOZC_DATA" "$APP/Contents/Resources/mozc.data"
cp ../../../src/data/preedit/romanji-hiragana.tsv "$APP/Contents/Resources/roman.tsv"
cp ../../../src/data/keymap/ms-ime.tsv "$APP/Contents/Resources/keymap.tsv"
# SET_CONFIG の session_keymap(ATOK/KOTOERI/MOBILE/CHROMEOS/MSIME)プリセットは
# <datadir>/keymap/<preset>.tsv から解決される(KeymapPresets.Load)。プリセット tsv 一式を
# Resources/keymap/ に同梱しないと、既定以外のキーマップへ切替できない(Linux deb と同じ)。
mkdir -p "$APP/Contents/Resources/keymap"
for f in ms-ime atok kotoeri mobile chromeos; do
  need "../../../src/data/keymap/$f.tsv"
  cp "../../../src/data/keymap/$f.tsv" "$APP/Contents/Resources/keymap/$f.tsv"
done
pkgbuild --root "$APP" --identifier org.mozc.inputmethod.Mozc \
  --version 1.0.0 --install-location "/Library/Input Methods/$APP" Mozc.pkg
