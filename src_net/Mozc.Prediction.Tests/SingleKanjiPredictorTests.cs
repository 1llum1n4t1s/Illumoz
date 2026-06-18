using System.Collections.Generic;
using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class SingleKanjiPredictorTests
{
    private static SingleKanjiPredictor Build() => new(
        new Dictionary<string, string[]>
        {
            ["あ"] = new[] { "亜", "阿", "唖" },
            ["あい"] = new[] { "愛", "藍", "相" },
        },
        generalSymbolId: 7);

    [Fact]
    public void Decode_ExactKey_ReturnsKanjiInOrder()
    {
        List<PredictionResult> r = Build().Decode("あい");
        var values = r.ConvertAll(x => x.Value);
        Assert.Equal(new[] { "愛", "藍", "相" }, values);
        Assert.All(r, x => Assert.Equal(7, x.Lid));
        // wcost はリスト順に増加。
        Assert.True(r[0].Wcost < r[1].Wcost && r[1].Wcost < r[2].Wcost);
    }

    [Fact]
    public void Decode_NoPartial_OnlyFullKey()
    {
        List<PredictionResult> r = Build().Decode("あい");
        Assert.DoesNotContain("亜", r.ConvertAll(x => x.Value)); // "あ" の候補は出ない
    }

    [Fact]
    public void Decode_AllowPartial_IncludesShorterKey()
    {
        List<PredictionResult> r = Build().Decode("あい", allowPartial: true);
        var values = r.ConvertAll(x => x.Value);
        Assert.Contains("愛", values); // あい
        Assert.Contains("亜", values); // あ(部分)
        // 短いキー候補は offset 加算で下位。
        int aiCost = r.Find(x => x.Value == "愛")!.Wcost;
        int aCost = r.Find(x => x.Value == "亜")!.Wcost;
        Assert.True(aiCost < aCost);
    }

    [Fact]
    public void Decode_Unknown_Empty()
    {
        Assert.Empty(Build().Decode("ぞ"));
    }

    [Fact]
    public void Decode_Empty_Empty()
    {
        Assert.Empty(Build().Decode(""));
    }
}
