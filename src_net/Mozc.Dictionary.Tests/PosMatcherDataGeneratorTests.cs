using System.Text.RegularExpressions;
using Mozc.Dictionary;
using Xunit;
using SysFile = System.IO.File;

namespace Mozc.Dictionary.Tests;

public class PosMatcherDataGeneratorTests
{
    [Fact]
    public void Generate_SyntheticData_RoundTripsThroughPosMatcher()
    {
        string[] idDef =
        {
            "# comment",
            "0 BOS/EOS,*,*,*,*,*,*",
            "10 助詞,格助詞,一般,*,*,*,が",
            "11 助詞,接続助詞,*,*,*,*,て",
            "20 名詞,固有名詞,人名,名,*,*,*",
            "21 名詞,固有名詞,人名,姓,*,*,*",
            "30 名詞,一般,*,*,*,*,*",
        };
        string[] specialPos = { "特殊,郵便番号" }; // → id 31
        string[] rules =
        {
            "Functional ^(助詞|助動詞)",
            "FirstName  名詞,固有名詞,人名,名",
            "LastName   名詞,固有名詞,人名,姓",
            "Zipcode    特殊,郵便番号",
        };

        var db = PosMatcherDataGenerator.ParsePosDatabase(idDef, specialPos);
        var ruleList = PosMatcherDataGenerator.ParseRules(rules);
        ushort[] data = PosMatcherDataGenerator.Generate(db, ruleList);

        var pm = new PosMatcher(data, ruleList.Count);
        Assert.Equal(10, pm.GetId(0)); // Functional GetId = 最小マッチ id
        Assert.True(pm.IsRuleInTable(0, 10));
        Assert.True(pm.IsRuleInTable(0, 11));
        Assert.False(pm.IsRuleInTable(0, 30));
        Assert.True(pm.IsRuleInTable(1, 20));   // FirstName
        Assert.False(pm.IsRuleInTable(1, 21));
        Assert.True(pm.IsRuleInTable(2, 21));   // LastName
        Assert.True(pm.IsRuleInTable(3, 31));   // Zipcode(special pos)
    }

    [Fact]
    public void ConsecutiveIds_GroupIntoSingleRange()
    {
        // 10,11,12 連続 → 1 レンジ(10,12)。13欠落,14 → 別レンジ。
        string[] idDef =
        {
            "10 助詞,a", "11 助詞,b", "12 助詞,c", "14 助詞,d",
        };
        var db = PosMatcherDataGenerator.ParsePosDatabase(idDef);
        var ranges = PosMatcherDataGenerator.GetRange(new Regex("助詞"), db);
        Assert.Equal(2, ranges.Count);
        Assert.Equal((10, 12), ranges[0]);
        Assert.Equal((14, 14), ranges[1]);
    }

    // 実 mozc ソースデータでの初検証(ファイルが見つかれば)。
    [Fact]
    public void RealData_GeneratesConsistentPosMatcher()
    {
        string? root = FindRepoRoot();
        if (root == null)
        {
            return; // リポジトリ外で実行された場合はスキップ。
        }
        string idDefPath = Path.Combine(root, "src/data/dictionary_oss/id.def");
        string specialPath = Path.Combine(root, "src/data/rules/special_pos.def");
        string rulePath = Path.Combine(root, "src/data/rules/pos_matcher_rule.def");
        if (!SysFile.Exists(idDefPath) || !SysFile.Exists(rulePath))
        {
            return;
        }

        var db = PosMatcherDataGenerator.ParsePosDatabase(
            SysFile.ReadLines(idDefPath),
            SysFile.Exists(specialPath) ? SysFile.ReadLines(specialPath) : null);
        var rules = PosMatcherDataGenerator.ParseRules(SysFile.ReadLines(rulePath));

        // 規則数は PosMatcher.RuleCount(35)と一致するはず。
        Assert.Equal(PosMatcher.RuleCount, rules.Count);

        ushort[] data = PosMatcherDataGenerator.Generate(db, rules);
        var pm = new PosMatcher(data, rules.Count);

        // 助詞 で始まる素性の id は Functional のはず。
        var joshi = db.FirstOrDefault(x => x.Feature.StartsWith("助詞"));
        Assert.NotEqual(default, joshi);
        Assert.True(pm.IsFunctional((ushort)joshi.Id));

        // 数詞: アラビア数字の id は Number。
        var arabic = db.FirstOrDefault(x => x.Feature.StartsWith("名詞,数,アラビア数字"));
        if (arabic != default)
        {
            Assert.True(pm.IsNumber((ushort)arabic.Id));
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (SysFile.Exists(Path.Combine(dir.FullName, "src/data/rules/pos_matcher_rule.def")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
