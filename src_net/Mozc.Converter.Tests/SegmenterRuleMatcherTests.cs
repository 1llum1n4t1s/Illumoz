using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

public class SegmenterRuleMatcherTests
{
    private static List<(string, int)> Db() => new()
    {
        ("BOS/EOS,*,*,*,*,*,*", 0),
        ("名詞,数,アラビア数字", 10),
        ("名詞,数,漢数字", 11),
        ("名詞,一般,*", 20),
        ("助詞,格助詞,一般", 30),
    };

    [Fact]
    public void BuildIsBoundary_AppliesRulesInOrder()
    {
        var db = Db();
        var rules = SegmenterRuleMatcher.ParseRules(new[]
        {
            "名詞,数,アラビア数字 名詞,数,(アラビア数字|漢数字) false",
            "名詞,数,漢数字 名詞,数,アラビア数字 true",
            "* 助詞,格助詞,一般 false",
        });
        var isBoundary = SegmenterRuleMatcher.BuildIsBoundary(db, rules);

        // BOS/EOS は常に境界。
        Assert.True(isBoundary(0, 10));
        Assert.True(isBoundary(10, 0));

        // 規則1: アラビア数字(10) → アラビア/漢数字(10 or 11) は false(連結)。
        Assert.False(isBoundary(10, 10));
        Assert.False(isBoundary(10, 11));

        // 規則2: 漢数字(11) → アラビア数字(10) は true。
        Assert.True(isBoundary(11, 10));

        // 規則3: * → 助詞格助詞(30) は false。
        Assert.False(isBoundary(20, 30));

        // どの規則にもマッチしない → default true。
        Assert.True(isBoundary(20, 20));
    }

    [Fact]
    public void StarPattern_MatchesAny()
    {
        var db = Db();
        var rules = SegmenterRuleMatcher.ParseRules(new[] { "* * false" });
        var isBoundary = SegmenterRuleMatcher.BuildIsBoundary(db, rules);
        // BOS/EOS 以外は全て規則(* *)で false。
        Assert.False(isBoundary(10, 20));
        Assert.False(isBoundary(20, 30));
        Assert.True(isBoundary(0, 20)); // BOS は別。
    }

    [Fact]
    public void EndToEnd_WithBitarrayGenerator()
    {
        // 規則→is_boundary→bitarray 圧縮→Segmenter で読み戻し一致。
        var db = Db();
        var rules = SegmenterRuleMatcher.ParseRules(new[]
        {
            "名詞,数,漢数字 名詞,数,アラビア数字 true",
            "* 助詞,格助詞,一般 false",
        });
        var isBoundary = SegmenterRuleMatcher.BuildIsBoundary(db, rules);
        int n = SegmenterRuleMatcher.PosCount(db); // 31

        var r = SegmenterBitarrayGenerator.Generate(n, n, isBoundary);
        var seg = new Segmenter(r.CompressedLSize, r.CompressedRSize,
            r.LTable, r.RTable, r.Bitarray, new ushort[2 * (n + 1)]);

        // rid>=1 は exact(rid=0 のみ番兵エイリアス)。代表ペアを確認。
        Assert.True(seg.IsBoundary(11, 10));   // 規則1 true
        Assert.False(seg.IsBoundary(20, 30));  // 規則2 false
        Assert.True(seg.IsBoundary(20, 20));   // default true
    }
}
