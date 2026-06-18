using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class UserHistoryPredictorTests
{
    [Fact]
    public void PredictZeroQuery_RecentFirst()
    {
        long clock = 100;
        var p = new UserHistoryPredictor(clock: () => clock);
        p.Learn("あ", "亜"); clock = 200;
        p.Learn("い", "胃"); clock = 300;
        p.Learn("う", "宇");

        var r = p.PredictZeroQuery();
        Assert.Equal("宇", r[0].Value); // 直近が先頭
        Assert.Equal("亜", r[^1].Value); // 最古が末尾
    }

    [Fact]
    public void Learn_ThenPredict_PrefixMatch()
    {
        var p = new UserHistoryPredictor();
        p.Learn("とうきょう", "東京");
        p.Learn("とうきょうと", "東京都");

        var r = p.Predict("とうきょう");
        var values = r.ConvertAll(x => x.Value);
        Assert.Contains("東京", values);
        Assert.Contains("東京都", values);
    }

    [Fact]
    public void Predict_RanksByFrequency()
    {
        var p = new UserHistoryPredictor();
        p.Learn("か", "下");
        p.Learn("か", "可");
        p.Learn("か", "可"); // 可=2回

        var r = p.Predict("か");
        Assert.Equal("可", r[0].Value); // 高頻度が先頭
        Assert.True(r[0].Cost <= r[1].Cost);
    }

    [Fact]
    public void Predict_NoMatch_Empty()
    {
        var p = new UserHistoryPredictor();
        p.Learn("あい", "愛");
        Assert.Empty(p.Predict("うえ"));
    }

    [Fact]
    public void Lru_EvictsOldest()
    {
        long clock = 100;
        var p = new UserHistoryPredictor(capacity: 2, clock: () => clock);
        p.Learn("a", "A"); clock = 200;
        p.Learn("b", "B"); clock = 300;
        p.Learn("c", "C"); // 容量2超過 → 最古 "A" が破棄

        Assert.Equal(2, p.Count);
        Assert.Empty(p.Predict("a"));
        Assert.NotEmpty(p.Predict("b"));
        Assert.NotEmpty(p.Predict("c"));
    }

    [Fact]
    public void RemoveAndClear()
    {
        var p = new UserHistoryPredictor();
        p.Learn("あい", "愛");
        p.Learn("こい", "恋");
        Assert.True(p.Remove("あい", "愛"));
        Assert.Empty(p.Predict("あい"));
        p.Clear();
        Assert.Equal(0, p.Count);
    }

    [Fact]
    public void SnapshotRestore_RoundTrip()
    {
        var p = new UserHistoryPredictor();
        p.Learn("ねこ", "猫");
        p.Learn("ねこ", "猫");
        var snap = new List<UserHistoryPredictor.Entry>(p.Snapshot());

        var p2 = new UserHistoryPredictor();
        p2.Restore(snap);
        var r = p2.Predict("ねこ");
        Assert.Single(r);
        Assert.Equal("猫", r[0].Value);
    }
}
