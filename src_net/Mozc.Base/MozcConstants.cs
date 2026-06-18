namespace Mozc.Base;

// C++ src/base/const.h 相当。OSS ビルド(Mozc)既定の定数。
// GoogleJapaneseInput ブランドは将来ビルド構成で切り替える(現状 OSS)。
public static class MozcConstants
{
    // kProductNameInEnglish (OSS) / Windows プロファイルのサブフォルダ名・mac の Application Support サブフォルダ名
    public const string ProductNameInEnglish = "Mozc";

    // kCompanyNameInEnglish (OSS)。Google ビルドのみ Windows でプロファイル親に挟む
    public const string CompanyNameInEnglish = "Mozc Project";

    // IPC プロトコルバージョン (ipc.h: IPC_PROTOCOL_VERSION)
    public const uint IpcProtocolVersion = 3;

    // IPC キーの長さ(16byte を hex 化した 32 文字)
    public const int IpcKeySize = 32;

    // Windows 名前付きパイプのプレフィックス (const.h: kIPCPrefix)。
    // .NET の NamedPipeClientStream には "\\.\pipe\" を除いた部分を渡すため、
    // プレフィックス本体(パイプ名の接頭辞)だけを保持する。
    public const string WindowsPipeNamePrefix = "mozc.";

    // Linux abstract socket / POSIX のベースプレフィックス (ipc_path_manager: "/tmp/.mozc.")
    public const string PosixIpcPrefix = "/tmp/.mozc.";

    // 主要バイナリ名 (const.h)
    public const string MozcServerNameWindows = "mozc_server.exe";
    public const string MozcServerNamePosix = "mozc_server";
    public const string MozcToolNameWindows = "mozc_tool.exe";
    public const string MozcToolNamePosix = "mozc_tool";
}
