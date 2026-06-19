#!/bin/sh
# macOS 配布(pkg)。Mozc.app(IMK bundle)を組み立て pkgbuild で .pkg 化(実機 macOS)。
set -e
APP="Mozc.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Info.plist "$APP/Contents/"
# native/build.sh は Mozc.Os.Mac を作業ディレクトリにして `-o Mozc` を出力する
# (= プロジェクトルート直下の Mozc.Os.Mac/Mozc)。native/ ではないので注意。
cp ../../Mozc.Os.Mac/Mozc "$APP/Contents/MacOS/"
# IMK stub は NativeAOT 共有ライブラリ Mozc.Os.Mac.dylib にリンクされるため同梱必須。
cp ../../Mozc.Os.Mac/bin/Release/net10.0/osx-arm64/publish/Mozc.Os.Mac.dylib "$APP/Contents/MacOS/"
cp ../../Mozc.Server.Host/bin/Release/net10.0/osx-arm64/publish/Mozc.Server.Host "$APP/Contents/MacOS/mozc_server"
# mozc_server は --data/--roman/--keymap が必須(Program.cs)。Linux パッケージと同様に
# 変換データ・ローマ字表・キーマップを bundle に同梱する(launcher がこのパスで起動できるように)。
MOZC_DATA="${MOZC_DATA:-../../Mozc.Server.Host/mozc.data}"
cp "$MOZC_DATA" "$APP/Contents/Resources/mozc.data"
cp ../../../src/data/preedit/romanji-hiragana.tsv "$APP/Contents/Resources/roman.tsv"
cp ../../../src/data/keymap/ms-ime.tsv "$APP/Contents/Resources/keymap.tsv"
pkgbuild --root "$APP" --identifier org.mozc.inputmethod.Mozc \
  --version 1.0.0 --install-location "/Library/Input Methods/$APP" Mozc.pkg
