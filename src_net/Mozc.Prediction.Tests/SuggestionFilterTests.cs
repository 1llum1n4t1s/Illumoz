using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class SuggestionFilterTests
{
    [Fact]
    public void ShouldSuppress_MatchesCaseInsensitive()
    {
        var f = new SuggestionFilter(new[] { "ngword", "禁止" });
        Assert.True(f.ShouldSuppress("ngword"));
        Assert.True(f.ShouldSuppress("NGWord"));
        Assert.True(f.ShouldSuppress("禁止"));
        Assert.False(f.ShouldSuppress("ok"));
    }

    [Fact]
    public void Apply_RemovesFilteredKeepsOrder()
    {
        var f = new SuggestionFilter(new[] { "bad" });
        var input = new List<PredictionResult>
        {
            new() { Value = "good1" },
            new() { Value = "bad" },
            new() { Value = "good2" },
        };
        var outp = f.Apply(input);
        Assert.Equal(new[] { "good1", "good2" }, outp.ConvertAll(x => x.Value));
    }

    [Fact]
    public void Empty_SuppressesNothing()
    {
        Assert.False(SuggestionFilter.Empty.ShouldSuppress("anything"));
    }
}
