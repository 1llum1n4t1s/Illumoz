// IpcPathManager 系テストは静的な MozcPaths.OverrideUserProfileDirectory を共有するため、
// 並列実行を無効化して競合(分離不全)を防ぐ。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
