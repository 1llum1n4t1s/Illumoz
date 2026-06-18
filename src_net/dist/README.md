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

## TSF TIP DLL の publish(Windows)
- `Mozc.Os.Windows` を NativeAOT 共有ライブラリとして publish: `dotnet publish Mozc.Os.Windows -r win-x64 -p:NativeLib=Shared -p:PublishAot=true`。
- エクスポートは `[UnmanagedCallersOnly(EntryPoint="DllGetClassObject"/"DllCanUnloadNow")]` で自動生成(`Mozc.Os.Windows.def` は export 面の文書/`/DEF` 併用も可)。
- 生成 DLL を `regsvr32` 相当のインストーラカスタムアクションで CLSID(10a67bc8…)登録 + `ITfInputProcessorProfiles::Register` でプロファイル登録(実機 Windows)。

## 実証(2026-06-19): TSF TIP DLL の NativeAOT 共有ライブラリ
- `dotnet publish Mozc.Os.Windows -r win-x64 -p:NativeLib=Shared -p:PublishAot=true` で **3.7MB のネイティブ COM DLL 生成成功**。
- `dumpbin /EXPORTS` で **`DllGetClassObject` / `DllCanUnloadNow` のエクスポートを確認**(COM 登録可能な実 TIP DLL)。
- 残: 実機 Windows でこの DLL を CLSID 登録 + `ITfInputProcessorProfiles::Register` し、任意アプリで入力動作を目視。

## 実証(2026-06-19): native bridge C ABI エクスポート
- `dotnet publish Mozc.Os.Linux -p:NativeLib=Shared -p:PublishAot=true` の共有ライブラリで
  `dumpbin /EXPORTS` により **`mozc_ibus_process_key` / `mozc_ibus_get_preedit` / `mozc_ibus_get_commit`** の
  エクスポートを確認(native ibus_mozc.c が解決する C ABI が実際に emit される)。
- 同手順で Linux は `.so`、mac は `Mozc.Os.Mac` を共有ライブラリ化(`mozc_imk_*` export)。
- → C#[UnmanagedCallersOnly] → 共有ライブラリ export → native 極薄 stub(.c/.m)→ OS、の経路が成立。
