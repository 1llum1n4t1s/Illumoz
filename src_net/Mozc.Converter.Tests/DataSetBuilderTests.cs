using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

// 究極の縦串: ソースデータ → DataSetBuilder → mozc.data → DataManager →
// ImmutableConverter → 変換。データ生成も消費も変換も C# だけで完結することを実証。
public class DataSetBuilderTests
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

    [Fact]
    public void SourcesToConversion_EndToEnd()
    {
        var sources = new DataSetBuilder.Sources
        {
            // 辞書: わたし=私(名詞1), の=の(助詞2), なまえ=名前(名詞1)。
            DictionaryLines = new[]
            {
                "わたし\t1\t1\t100\t私",
                "の\t2\t2\t50\tの",
                "なまえ\t1\t1\t100\t名前",
            },
            // 連接: pos_size=3, 全 0(BOS/EOS/全遷移 0)。
            ConnectionLines = new[] { "3", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[]
            {
                "0 BOS/EOS,*,*,*,*,*,*",
                "1 名詞,一般,*,*,*,*,*",
                "2 助詞,格助詞,一般,*,*,*,*",
            },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            // 文法境界: default true なので各ノードが文節になる。
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = new[] { "PREFIX 助詞,格助詞, 1000" },
        };

        byte[] mozcData = new DataSetBuilder().Build(sources);

        // DataManager で読み、ImmutableConverter で変換。
        var dm = new DataManager(mozcData);
        PosMatcher pos = dm.GetPosMatcher();
        var conv = new ImmutableConverter(
            dm.GetSystemDictionary(), dm.GetConnector(), dm.GetSegmenter(), pos, new CandidateFilter(pos));

        Segments segments = conv.Convert("わたしのなまえ");

        Assert.Equal(3, segments.ConversionSegmentsSize);
        Assert.Equal("私", segments.ConversionSegment(0).Get(0).Value);
        Assert.Equal("の", segments.ConversionSegment(1).Get(0).Value);
        Assert.Equal("名前", segments.ConversionSegment(2).Get(0).Value);

        // pos_matcher も生きている(助詞 id2 は Functional)。
        Assert.True(pos.IsFunctional(2));
        Assert.False(pos.IsFunctional(1));
    }
}
