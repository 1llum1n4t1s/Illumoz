namespace Mozc.Base;

// プロファイル(設定/履歴/ユーザー辞書/文字形)の永続化をアトミックに行うユーティリティ。
// File.WriteAllBytes の直書きは、保存中の電源断/OOM/SIGKILL で途中書き(0バイト or 切り詰め)
// 破損を残す。同一ディレクトリへ一時ファイルを書いてから rename(置換)することで、
// 「完全な旧版」か「完全な新版」のどちらかだけが残るようにし、サイレント全損を防ぐ。
public static class AtomicFile
{
    // path へ data をアトミックに書き込む(temp 書き込み → flush → 置換 rename)。
    public static void WriteAllBytes(string path, byte[] data)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        // 同一ディレクトリに作る(rename を同一ボリューム内のアトミック操作にするため)。
        string tmp = path + ".tmp-" + Environment.ProcessId.ToString();
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
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
