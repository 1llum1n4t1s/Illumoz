# 配布(C7)成果物

C++/Bazel を使わず .NET 10 / NativeAOT で各 OS に配布するための定義。

- `ibus/mozc.xml` — Linux ibus component 定義(`/usr/share/ibus/component/` に配置)。
- `mac/Info.plist` — macOS IMKit 入力メソッド bundle の Info.plist(`Mozc.app/Contents/`)。
- `windows/Mozc.wxs` — Windows MSI(WiX v4)。`PublishDir` に NativeAOT publish 出力を指す。

## ビルド/配布フロー(実機)
1. `dotnet publish` を各 RID で実行(`Directory.Build.props` の `IsAotCompatible`、exe 側で `PublishAot=true`)。
   - Windows: `Mozc.Os.Windows`(TIP DLL) / `Mozc.Server.Host` / `Mozc.Gui.App`、`Mozc.DataGen` で `mozc.data` 生成。
   - Linux: `Mozc.Server.Host` + ibus エンジン、`Mozc.DataGen`。
   - macOS: `Mozc.Server.Host` + IMK bundle、`Mozc.DataGen`。
2. Windows は WiX で MSI、macOS は `.pkg`(各 `.app` + launchd)、Linux は deb/zip(ibus component.xml 同梱)。
3. TIP/IME 登録は各 OS のカスタムアクション/インストールスクリプトで(`ITfInputProcessorProfiles::Register` 等)。

注: native 極薄シムの登録・実 IME 動作確認は実機 Windows/mac/Linux が必須。

## 実証済み(2026-06-19)
- `dotnet publish Mozc.DataGen -r win-x64 -p:PublishAot=true` で **NativeAOT ネイティブ exe(約4.3MB, .NET ランタイム不要)生成成功**(VS の vswhere を PATH に通せばリンク成立)。
- その AOT exe で実 src/data 全量から **mozc.data(13,550,114 bytes)を生成**、**JIT 版とバイト完全一致**(sha1 b98094a1…)=データ生成は決定的。
- → Bazel 無し・C++ 無しで「ソース→(NativeAOT 自己完結 exe)→mozc.data→変換」が成立。
