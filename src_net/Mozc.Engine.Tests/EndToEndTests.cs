using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Xunit;

namespace Mozc.Engine.Tests;

// 究極の縦串(C# 単独): ローマ字打鍵 → Composer かな変換 → mozc.data 変換 → 候補。
public class EndToEndTests
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

    private static byte[] BuildData()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[]
            {
                "わたし\t1\t1\t100\t私",
                "の\t2\t2\t50\tの",
                "なまえ\t1\t1\t100\t名前",
            },
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
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = new[] { "PREFIX 助詞,格助詞, 1000" },
        };
        return new DataSetBuilder().Build(sources);
    }

    private const string RomanTable =
        "wa\tわ\nta\tた\nshi\tし\nno\tの\nna\tな\nma\tま\ne\tえ\nn\tん";

    [Fact]
    public void Romaji_To_Kana_To_Conversion()
    {
        var engine = new MozcEngine(BuildData(), RomanTable);

        var composer = engine.CreateComposer();
        composer.InsertCharacters("watashinonamae");

        // Composer がローマ字をかなに。
        Assert.Equal("わたしのなまえ", composer.GetQueryForConversion());

        // mozc.data 変換で 私/の/名前。
        Segments segments = engine.ConvertFromComposer(composer);
        Assert.Equal(3, segments.ConversionSegmentsSize);
        Assert.Equal("私", segments.ConversionSegment(0).Get(0).Value);
        Assert.Equal("の", segments.ConversionSegment(1).Get(0).Value);
        Assert.Equal("名前", segments.ConversionSegment(2).Get(0).Value);
    }
}
