using Mozc.Ipc;
using Xunit;

namespace Mozc.Ipc.Tests;

// ServerLauncher の server_status ラッチ + ポーリングを、依存(可用性判定/spawn/待機)を
// 注入して検証する(実プロセス/実ファイルに触れない)。internal ctor を InternalsVisibleTo 経由で使う。
public class ServerLauncherTests
{
    [Fact]
    public void AlreadyAvailable_DoesNotSpawn()
    {
        int spawns = 0;
        var l = new ServerLauncher(
            isAvailable: () => true,
            spawn: () => { spawns++; return true; },
            wait: () => { },
            trials: 20);

        Assert.True(l.EnsureServerRunning());
        Assert.Equal(0, spawns); // 既に公開済みなら起動しない
        Assert.Equal(ServerLauncher.Status.Running, l.CurrentStatus);
    }

    [Fact]
    public void SpawnsThenBecomesAvailable_AfterPolling()
    {
        int spawns = 0;
        int waits = 0;
        int availChecks = 0;
        var l = new ServerLauncher(
            // 初回(spawn 前)false、spawn 後の 3 回目のポーリングで true。
            isAvailable: () => ++availChecks >= 4,
            spawn: () => { spawns++; return true; },
            wait: () => waits++,
            trials: 20);

        Assert.True(l.EnsureServerRunning());
        Assert.Equal(1, spawns);                 // 1 度だけ起動
        Assert.True(waits >= 1);                  // 公開されるまで待機した
        Assert.Equal(ServerLauncher.Status.Running, l.CurrentStatus);
    }

    [Fact]
    public void SpawnFails_LatchesFatal_AndDoesNotRetry()
    {
        int spawns = 0;
        var l = new ServerLauncher(
            isAvailable: () => false,
            spawn: () => { spawns++; return false; }, // 起動できない
            wait: () => { },
            trials: 20);

        Assert.False(l.EnsureServerRunning());
        Assert.Equal(ServerLauncher.Status.Fatal, l.CurrentStatus);
        // FATAL ラッチ後は再試行しない(respawn storm 防止)。
        Assert.False(l.EnsureServerRunning());
        Assert.Equal(1, spawns);
    }

    [Fact]
    public void NeverBecomesAvailable_LatchesFatalAfterTrials()
    {
        int waits = 0;
        var l = new ServerLauncher(
            isAvailable: () => false,            // 起動はするが .ipc が一向に公開されない
            spawn: () => true,
            wait: () => waits++,
            trials: 5);

        Assert.False(l.EnsureServerRunning());
        Assert.Equal(5, waits);                  // trials 回だけ待機
        Assert.Equal(ServerLauncher.Status.Fatal, l.CurrentStatus);
    }
}
