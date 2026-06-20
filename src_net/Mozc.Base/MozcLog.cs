namespace Mozc.Base;

// 重要エラー経路(IPC障害/config破損/ハンドラ例外/プロファイル保存失敗)の最小ログ。
// 常駐デスクトップ IME のため集中ログ基盤は持たず、既定で stderr に1行出すだけ。
// 既定は無効(MOZC_VERBOSE=1 で有効化)。サポート診断時にユーザーが env を立てて再現させる。
//
// PII 配慮: 変換テキスト(preedit/commit = ユーザーの入力内容)を本文へ出さない。
// 文字列の中身が必要なときは Redact(s) で「長さのみ」に丸めること。
public static class MozcLog
{
    // MOZC_VERBOSE が "1"/"true" のときだけログを出す。プロセス起動時に1回評価。
    private static readonly bool Enabled = IsVerboseEnabled();

    private static bool IsVerboseEnabled()
    {
        string? v = Environment.GetEnvironmentVariable("MOZC_VERBOSE");
        return v is "1" or "true" or "TRUE";
    }

    // エラーを stderr に1行記録する(例外メッセージのみ。スタックや入力内容は出さない)。
    public static void Error(string where, Exception ex)
        => Write($"[mozc] {where}: {ex.GetType().Name}: {ex.Message}");

    public static void Error(string message) => Write($"[mozc] {message}");

    // PII を避けるため、文字列を「<len=N>」表記へ丸める(中身は出さない)。
    public static string Redact(string? s) => s is null ? "<null>" : $"<len={s.Length}>";

    private static void Write(string line)
    {
        if (!Enabled)
        {
            return;
        }
        try
        {
            Console.Error.WriteLine(line);
        }
        catch
        {
            // ログ出力自体の失敗で本処理を妨げない。
        }
    }
}
