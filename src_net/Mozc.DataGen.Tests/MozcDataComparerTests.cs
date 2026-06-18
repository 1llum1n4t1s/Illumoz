using Mozc.Converter;
using Mozc.DataGen;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.DataGen.Tests;

public class MozcDataComparerTests
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

    private static byte[] Build(string firstValue)
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { $"わたし\t1\t1\t100\t{firstValue}" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = global::System.Array.Empty<string>(),
        };
        return new DataSetBuilder().Build(sources);
    }

    [Fact]
    public void IdenticalSources_ProduceIdenticalData()
    {
        byte[] a = Build("私");
        byte[] b = Build("私");
        MozcDataComparer.Report r = MozcDataComparer.Compare(a, b);
        Assert.True(r.Identical, MozcDataComparer.Format(r));
        Assert.Equal(MozcDataComparer.Sha1(a), MozcDataComparer.Sha1(b));
        Assert.Empty(r.Mismatches);
    }

    [Fact]
    public void DifferentDict_ShowsDictSectionDiff()
    {
        MozcDataComparer.Report r = MozcDataComparer.Compare(Build("私"), Build("渡"));
        Assert.False(r.Identical);
        // dict 節が差分として現れる(conn/pos_matcher/segmenter は同一)。
        Assert.Contains(r.Sections, s => s.Name == "dict" && !s.BytesEqual);
        Assert.Contains(r.Sections, s => s.Name == "conn" && s.BytesEqual);
    }
}
