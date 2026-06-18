using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class DictionaryPredictorTests
{
    private static List<string> PosRules()
    {
        var rules = new List<string> { "Functional ^助詞" };
        for (int i = 1; i < PosMatcher.RuleCount; i++)
        {
            rules.Add($"R{i} ^ZZ{i}");
        }
        return rules;
    }

    private static DataManager BuildManager()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[]
            {
                "とうきょう\t1\t1\t100\t東京",
                "とうほく\t1\t1\t200\t東北",
                "とうきょうと\t1\t1\t300\t東京都",
            },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[]
            {
                "0 BOS/EOS,*,*,*,*,*,*",
                "1 名詞,一般,*,*,*,*,*",
            },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = global::System.Array.Empty<string>(),
        };
        return new DataManager(new DataSetBuilder().Build(sources));
    }

    private static DictionaryPredictor Predictor(DataManager dm)
        => new(dm.GetSystemDictionary(), dm.GetConnector(), dm.GetSegmenter());

    [Fact]
    public void Predict_RanksByLengthBonusAndCost()
    {
        DictionaryPredictor p = Predictor(BuildManager());
        List<PredictionResult> results = p.Predict("とう");

        var values = results.ConvertAll(r => r.Value);
        Assert.Equal(new[] { "東京", "東京都", "東北" }, values);
        // コストは昇順。
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Cost <= results[i].Cost);
        }
    }

    [Fact]
    public void Predict_NarrowerQuery_FiltersPrefix()
    {
        DictionaryPredictor p = Predictor(BuildManager());
        List<PredictionResult> results = p.Predict("とうきょう");
        var values = results.ConvertAll(r => r.Value);
        Assert.Contains("東京", values);
        Assert.Contains("東京都", values);
        Assert.DoesNotContain("東北", values);
    }

    [Fact]
    public void Predict_EmptyQuery_ReturnsNothing()
    {
        DictionaryPredictor p = Predictor(BuildManager());
        Assert.Empty(p.Predict(""));
    }
}
