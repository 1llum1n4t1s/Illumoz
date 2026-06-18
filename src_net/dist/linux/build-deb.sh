#!/bin/sh
# Linux 配布(deb)。NativeAOT publish 成果物 + ibus stub + component.xml をパッケージ化(実機)。
set -e
STAGE="${1:-stage}"
mkdir -p "$STAGE/usr/lib/ibus-mozc" "$STAGE/usr/lib/mozc" \
         "$STAGE/usr/share/ibus/component" "$STAGE/DEBIAN"
cp ../../Mozc.Server.Host/bin/Release/net10.0/linux-x64/publish/Mozc.Server.Host "$STAGE/usr/lib/mozc/mozc_server"
cp ../../Mozc.Os.Linux/native/ibus-engine-mozc "$STAGE/usr/lib/ibus-mozc/" 2>/dev/null || true
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
