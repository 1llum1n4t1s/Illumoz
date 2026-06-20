using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class AtomicFileTests
{
    private static string TempPath()
        => Path.Combine(Path.GetTempPath(), "mozc_atomic_" + Guid.NewGuid().ToString("N") + ".bin");

    [Fact]
    public void WriteAllBytes_RoundTrips()
    {
        string path = TempPath();
        try
        {
            byte[] data = { 1, 2, 3, 4, 5 };
            AtomicFile.WriteAllBytes(path, data);
            Assert.Equal(data, File.ReadAllBytes(path));
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void WriteAllBytes_ConcurrentSamePath_LeavesCompleteVersion()
    {
        // 同一パスへの並行フラッシュ(例: SYNC_DATA 保存 vs ProcessExit 保存)でも、呼び出し毎に
        // 一意な temp 名を使うため互いの temp を truncate/move/delete せず、最終ファイルは常に
        // 「完全な版」のまま。最終 rename の競合は OS 依存で個別に失敗しうるが、呼び出し側
        // (ServerHost.SaveQuietly)が握る best-effort なので、ここでも個別失敗は許容する。
        string path = TempPath();
        try
        {
            byte[] a = Enumerable.Repeat((byte)1, 1000).ToArray();
            byte[] b = Enumerable.Repeat((byte)2, 2000).ToArray();
            Parallel.For(0, 32, i =>
            {
                try
                {
                    AtomicFile.WriteAllBytes(path, (i % 2 == 0) ? a : b);
                }
                catch (IOException) { /* 最終 rename の競合は許容(best-effort) */ }
                catch (UnauthorizedAccessException) { /* 同上(Windows の同時 rename) */ }
            });
            // 少なくとも 1 回は成功してファイルが残り、その内容は途中書きでなく完全な a か b。
            byte[] got = File.ReadAllBytes(path);
            Assert.True(got.SequenceEqual(a) || got.SequenceEqual(b));
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void WriteAllBytes_OverwritesExisting_NoLeftoverTemp()
    {
        // 上書き保存後、temp が残らず(後始末)、最新内容が反映される。
        string path = TempPath();
        try
        {
            AtomicFile.WriteAllBytes(path, new byte[] { 1 });
            AtomicFile.WriteAllBytes(path, new byte[] { 2, 2 });
            Assert.Equal(new byte[] { 2, 2 }, File.ReadAllBytes(path));
            string? dir = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            Assert.Empty(Directory.GetFiles(dir!, name + ".tmp-*"));
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void WriteAllBytes_Unix_CreatesOwnerOnlyFile()
    {
        // POSIX のみ: 平文の履歴/辞書/設定を他ユーザーに読まれないよう 0600 で作る。
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        string path = TempPath();
        try
        {
            AtomicFile.WriteAllBytes(path, new byte[] { 9 });
            UnixFileMode mode = File.GetUnixFileMode(path);
            UnixFileMode group = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;
            UnixFileMode other = UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            Assert.Equal((UnixFileMode)0, mode & (group | other)); // group/other に権限が無い
            Assert.True((mode & UnixFileMode.UserRead) != 0);
            Assert.True((mode & UnixFileMode.UserWrite) != 0);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }
}
