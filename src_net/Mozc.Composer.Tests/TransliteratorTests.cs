using Mozc.Composer;
using Xunit;

namespace Mozc.Composer.Tests;

public class TransliteratorTests
{
    private static Table RealRomanTable()
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        string rel = "src/data/preedit/romanji-hiragana.tsv";
        while (dir != null && !global::System.IO.File.Exists(global::System.IO.Path.Combine(dir.FullName, rel)))
        {
            dir = dir.Parent;
        }
        var t = new Table();
        t.LoadFromString(global::System.IO.File.ReadAllText(
            global::System.IO.Path.Combine(dir!.FullName, rel)));
        return t;
    }

    [Fact]
    public void TypedViews_OfHiraganaInput()
    {
        Table t = RealRomanTable();
        var c = new Composer(t);
        c.InsertCharacters("nihongo"); // にほんご(末尾が完結モーラ)
        Assert.Equal("にほんご", c.GetStringForPreedit());
        Assert.Equal("にほんご", c.GetHiragana());
        Assert.Equal("ニホンゴ", c.GetFullKatakana());
        Assert.Equal("ﾆﾎﾝｺﾞ", c.GetHalfKatakana());
    }

    [Fact]
    public void AsciiViews_FromRaw()
    {
        Table t = RealRomanTable();
        var c = new Composer(t);
        // 未確定のローマ字 " k" は raw="k" を保持 → 半角/全角英数ビューで k / ｋ。
        c.InsertCharacters("k");
        Assert.Equal("k", c.GetHalfAscii());
        Assert.Equal("ｋ", c.GetFullAscii());
    }
}
