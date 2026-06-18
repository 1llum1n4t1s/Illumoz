using Mozc.Converter;
using Mozc.DataGen;
using Mozc.Dictionary;
using Mozc.Engine;
using Xunit;

namespace Mozc.DataGen.Tests;

// C7: src/data ソースファイル → mozc.data 生成(Bazel 不使用)を検証し、
// 生成物が DataManager/Engine で実際に変換できることまで確認する。
public class DataGeneratorTests
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
    public void FilesToData_ToConversion()
    {
        string dir = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), "mozcgen_" + global::System.Guid.NewGuid().ToString("N"));
        global::System.IO.Directory.CreateDirectory(dir);
        try
        {
            string Write(string name, IEnumerable<string> lines)
            {
                string p = global::System.IO.Path.Combine(dir, name);
                global::System.IO.File.WriteAllLines(p, lines);
                return p;
            }

            string d0 = Write("dict00.txt", new[] { "わたし\t1\t1\t100\t私", "の\t2\t2\t50\tの" });
            string d1 = Write("dict01.txt", new[] { "なまえ\t1\t1\t100\t名前" });
            string conn = Write("conn.txt", new[] { "3", "0", "0", "0", "0", "0", "0", "0", "0", "0" });
            string idDef = Write("id.def", new[]
            {
                "0 BOS/EOS,*,*,*,*,*,*",
                "1 名詞,一般,*,*,*,*,*",
                "2 助詞,格助詞,一般,*,*,*,*",
            });
            string posRule = Write("pos_matcher_rule.def", PosRules());
            string boundary = Write("boundary.def", new[] { "PREFIX 助詞,格助詞, 1000" });

            byte[] data = DataGenerator.Generate(new DataGenerator.FileSources
            {
                DictionaryFiles = new[] { d0, d1 },
                ConnectionFile = conn,
                ConnectionSpecialPosSize = 0,
                IdDefFile = idDef,
                PosMatcherRuleFile = posRule,
                BoundaryDefFile = boundary,
            });

            Assert.True(data.Length > 0);

            // 生成 mozc.data を Engine に流して変換できる。
            var engine = new MozcEngine(data, "wa\tわ\nta\tた\nshi\tし\nno\tの\nna\tな\nma\tま\ne\tえ\nn\tん");
            var composer = engine.CreateComposer();
            composer.InsertCharacters("watashinonamae");
            Segments segs = engine.ConvertFromComposer(composer);
            Assert.Equal(3, segs.ConversionSegmentsSize);
            Assert.Equal("私", segs.ConversionSegment(0).Get(0).Value);
            Assert.Equal("名前", segs.ConversionSegment(2).Get(0).Value);
        }
        finally
        {
            global::System.IO.Directory.Delete(dir, recursive: true);
        }
    }
}
