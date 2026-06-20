namespace Mozc.Base;

// プロファイル(設定/履歴/ユーザー辞書/文字形)の永続化をアトミックに行うユーティリティ。
// File.WriteAllBytes の直書きは、保存中の電源断/OOM/SIGKILL で途中書き(0バイト or 切り詰め)
// 破損を残す。同一ディレクトリへ一時ファイルを書いてから rename(置換)することで、
// 「完全な旧版」か「完全な新版」のどちらかだけが残るようにし、サイレント全損を防ぐ。
public static class AtomicFile
{
    // 同一プロセス内の複数フラッシュ(例: SYNC_DATA 保存と ProcessExit 保存)が同じ
    // 出力ファイルへ同時書き込みしても temp が衝突しないよう、呼び出し毎に増やす連番。
    private static int _tempCounter;

    // 履歴/ユーザー辞書/設定は入力した読み・語を平文で保持する。POSIX の既定 umask(022)だと
    // temp が world-readable で作られ rename がそのモードを引き継ぐため、所有者のみ(0600)で作る。
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    // path へ data をアトミックに書き込む(temp 書き込み → flush → 置換 rename)。
    public static void WriteAllBytes(string path, byte[] data)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        // 同一ディレクトリに、呼び出し毎に一意な temp 名で作る(rename を同一ボリューム内の
        // アトミック操作にしつつ、並行保存どうしが互いの temp を truncate/move/delete しないように
        // ProcessId + プロセス内連番で衝突を避ける)。
        int seq = System.Threading.Interlocked.Increment(ref _tempCounter);
        string tmp = $"{path}.tmp-{Environment.ProcessId}-{seq}";
        try
        {
            // UnixCreateMode で作成時から 0600 にする(Windows では無視される)。rename 後も維持。
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerOnly;
            }
            using (var fs = new FileStream(tmp, options))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(flushToDisk: true);
            }
            // 既存があれば上書き置換。Windows/Unix とも同一ボリューム内 rename は原子的。
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // 失敗時は一時ファイルを残さない(次回の書き込みを汚さない)。
            try
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }
            }
            catch
            {
                // 後始末の失敗は無視(本来の例外を優先)。
            }
            throw;
        }
    }
}
