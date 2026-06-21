#!/bin/sh
# macOS IMK 極薄 native stub のビルド(実機 macOS)。
# C# 側を `dotnet publish Mozc.Os.Mac -r osx-arm64 -p:NativeLib=Shared -p:PublishAot=true`
# で libMozc.Os.Mac.dylib にしてから、ObjC stub をコンパイルして Mozc.app に同梱する。
set -e
PUBLISH="${1:-bin/Release/net10.0/osx-arm64/publish}"
clang -framework Cocoa -framework InputMethodKit \
  -fobjc-arc -O2 \
  native/MozcImkController.m \
  "$PUBLISH/Mozc.Os.Mac.dylib" \
  -o Mozc
# 配置: Mozc.app/Contents/MacOS/Mozc + Info.plist(dist/mac/Info.plist) + launchd
echo "built Mozc (IMK input method)"
