using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class UserHistoryStorageTests
{
    [Fact]
    public void SerializeLoad_RoundTrip()
    {
        var p = new UserHistoryPredictor();
        p.Learn("とうきょう", "東京");
        p.Learn("とうきょう", "東京"); // freq=2
        p.Learn("おおさか", "大阪");

        byte[] bytes = UserHistoryStorage.Serialize(p);

        var p2 = new UserHistoryPredictor();
        Assert.True(UserHistoryStorage.Load(p2, bytes));
        Assert.Equal(2, p2.Count);

        var r = p2.Predict("とうきょう");
        Assert.Single(r);
        Assert.Equal("東京", r[0].Value);
    }

    [Fact]
    public void Deterministic_SameContentSameBytes()
    {
        var a = new UserHistoryPredictor(clock: () => 42);
        a.Learn("b", "B");
        a.Learn("a", "A");
        var b = new UserHistoryPredictor(clock: () => 42);
        b.Learn("a", "A");
        b.Learn("b", "B");
        Assert.Equal(UserHistoryStorage.Serialize(a), UserHistoryStorage.Serialize(b));
    }

    [Fact]
    public void Load_RejectsBadMagic()
    {
        var p = new UserHistoryPredictor();
        Assert.False(UserHistoryStorage.Load(p, new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }));
    }

    [Fact]
    public void SaveLoadFile_RoundTrip()
    {
        string id = global::System.Guid.NewGuid().ToString("N");
        string path = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), $"mozc_hist_{id}.db");
        try
        {
            var p = new UserHistoryPredictor();
            p.Learn("ねこ", "猫");
            UserHistoryStorage.Save(p, path);

            var p2 = new UserHistoryPredictor();
            Assert.True(UserHistoryStorage.LoadFile(p2, path));
            Assert.NotEmpty(p2.Predict("ねこ"));
        }
        finally
        {
            if (global::System.IO.File.Exists(path)) { global::System.IO.File.Delete(path); }
        }
    }
}
