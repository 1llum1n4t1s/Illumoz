#!/bin/sh
# macOS 配布(pkg)。Mozc.app(IMK bundle)を組み立て pkgbuild で .pkg 化(実機 macOS)。
set -e
APP="Mozc.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Info.plist "$APP/Contents/"
cp ../../Mozc.Os.Mac/Mozc "$APP/Contents/MacOS/" 2>/dev/null || true
cp ../../Mozc.Server.Host/bin/Release/net10.0/osx-arm64/publish/Mozc.Server.Host "$APP/Contents/MacOS/mozc_server" 2>/dev/null || true
pkgbuild --root "$APP" --identifier org.mozc.inputmethod.Mozc \
  --version 1.0.0 --install-location "/Library/Input Methods/$APP" Mozc.pkg
