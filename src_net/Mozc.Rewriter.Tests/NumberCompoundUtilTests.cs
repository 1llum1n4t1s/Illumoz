using Mozc.Rewriter;
using Xunit;
using ST = Mozc.Rewriter.NumberCompoundUtil.ScriptType;

namespace Mozc.Rewriter.Tests;

public class NumberCompoundUtilTests
{
    [Theory]
    [InlineData("100階", "100", "階", ST.HalfWidthArabic)]
    [InlineData("三人", "三", "人", ST.Kanji)]
    [InlineData("百舌鳥", "百", "舌鳥", ST.Kanji)]
    [InlineData("１２個", "１２", "個", ST.FullWidthArabic)]
    [InlineData("弐千円", "弐千", "円", ST.OldKanji | ST.Kanji)] // 弐(大字)+千(漢)→ 円が助数詞
    public void Split_NumberAndSuffix(string input, string number, string suffix, ST type)
    {
        (string n, string s, ST t) = NumberCompoundUtil.Split(input);
        Assert.Equal(number, n);
        Assert.Equal(suffix, s);
        Assert.Equal(type, t);
    }

    [Fact]
    public void Split_NoNumber_EmptyNumberPart()
    {
        (string n, string s, ST t) = NumberCompoundUtil.Split("あい");
        Assert.Equal("", n);
        Assert.Equal("あい", s);
        Assert.Equal(ST.None, t);
    }

    [Fact]
    public void Split_AllNumber_EmptySuffix()
    {
        (string n, string s, _) = NumberCompoundUtil.Split("123");
        Assert.Equal("123", n);
        Assert.Equal("", s);
    }
}
