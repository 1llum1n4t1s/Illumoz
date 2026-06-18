using System.Collections.Generic;
using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class PredictionResultFilterTests
{
    [Fact]
    public void MaybeRedundant_SameValue_True()
    {
        Assert.True(PredictionResultFilter.MaybeRedundant("とうきょう", "東京", "とうきょうと", "東京"));
    }

    [Fact]
    public void MaybeRedundant_SameKeyDifferentValue_False()
    {
        Assert.False(PredictionResultFilter.MaybeRedundant("とう", "東", "とう", "塔"));
    }

    [Fact]
    public void MaybeRedundant_OrdinarySuffix_True()
    {
        // "東京" の追記 "タワー"(カタカナ) → 冗長。
        Assert.True(PredictionResultFilter.MaybeRedundant("とうきょう", "東京", "とうきょうたわー", "東京タワー"));
    }

    [Fact]
    public void MaybeRedundant_EmojiSuffix_False()
    {
        // "東京" の追記 "🗼"(絵文字) → 非冗長。
        Assert.False(PredictionResultFilter.MaybeRedundant("とうきょう", "東京", "とうきょうたわー", "東京\U0001F5FC"));
    }

    [Fact]
    public void MaybeRedundant_NotPrefix_False()
    {
        Assert.False(PredictionResultFilter.MaybeRedundant("とう", "東", "なに", "名前"));
    }

    [Fact]
    public void Dedup_RemovesDuplicateValuesAndRedundant()
    {
        var input = new List<PredictionResult>
        {
            new() { Key = "とうきょう", Value = "東京" },
            new() { Key = "とうきょう", Value = "東京" },         // 同一値 → 除外
            new() { Key = "とうきょうたわー", Value = "東京タワー" }, // 冗長 → 除外
            new() { Key = "とうほく", Value = "東北" },           // 残る
        };
        List<PredictionResult> kept = PredictionResultFilter.Dedup(input);
        var values = kept.ConvertAll(r => r.Value);
        Assert.Equal(new[] { "東京", "東北" }, values);
    }
}
