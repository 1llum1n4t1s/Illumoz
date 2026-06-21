using Mozc.Composer;
using Xunit;

namespace Mozc.Composer.Tests;

public class ComposerConversionTests
{
    private static Table RealRomanTable()
    {
        string path = FindRepoFile("src/data/preedit/romanji-hiragana.tsv");
        var t = new Table();
        t.LoadFromString(global::System.IO.File.ReadAllText(path));
        return t;
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = global::System.IO.Path.Combine(dir.FullName, relative);
            if (global::System.IO.File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new global::System.IO.FileNotFoundException(relative);
    }

    private static string Preedit(Table t, string keys)
    {
        var c = new Composer(t);
        c.InsertCharacters(keys);
        return c.GetStringForPreedit();
    }

    private static string Query(Table t, string keys)
    {
        var c = new Composer(t);
        c.InsertCharacters(keys);
        return c.GetQueryForConversion();
    }

    [Fact]
    public void GetRawString_ReturnsTypedRomaji()
    {
        Table t = RealRomanTable();
        var c = new Composer(t);
        c.InsertCharacters("watashi");
        Assert.Equal("watashi", c.GetRawString());      // 打鍵そのもの
        Assert.Equal("わたし", c.GetStringForPreedit()); // 表示はかな
    }

    [Fact]
    public void GetQueryForPrediction_TrimsTrailingPending()
    {
        Table t = RealRomanTable();
        var c = new Composer(t);
        c.InsertCharacters("kan"); // "か" + pending "n"
        // 予測クエリは末尾 pending をトリム → "か"
        Assert.Equal("か", c.GetQueryForPrediction());
    }

    [Theory]
    [InlineData("watashi", "わたし")]
    [InlineData("nihongo", "にほんご")]
    [InlineData("gakkou", "がっこう")]
    [InlineData("konnnichiha", "こんにちは")]
    [InlineData("kanji", "かんじ")]
    [InlineData("toukyou", "とうきょう")]
    public void RealTable_FullWords_Convert(string keys, string expected)
    {
        Table t = RealRomanTable();
        Assert.Equal(expected, Preedit(t, keys));
        Assert.Equal(expected, Query(t, keys));
    }

    [Fact]
    public void TrailingN_PreeditKeepsRomaji_QueryFixes()
    {
        Table t = RealRomanTable();
        // 単独 "n" は preedit ではローマ字のまま、変換クエリでは "ん" に確定。
        Assert.Equal("n", Preedit(t, "n"));
        Assert.Equal("ん", Query(t, "n"));
    }

    [Fact]
    public void Gemination_SmallTable()
    {
        var t = new Table();
        t.LoadFromString("ka\tか\nkk\tっ\tk");
        Assert.Equal("っか", Preedit(t, "kka"));
    }

    [Fact]
    public void Reset_ClearsComposition()
    {
        Table t = RealRomanTable();
        var c = new Composer(t);
        c.InsertCharacters("watashi");
        c.Reset();
        Assert.Equal("", c.GetStringForPreedit());
    }
}
