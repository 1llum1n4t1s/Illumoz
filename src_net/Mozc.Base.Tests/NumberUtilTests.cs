using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class NumberUtilTests
{
    [Theory]
    [InlineData("三", "3")]
    [InlineData("十三", "103")]        // 十→10, 三→3(部分文字列置換)
    [InlineData("一二三", "123")]
    [InlineData("零", "0")]
    [InlineData("壱弐参", "123")]       // 大字
    [InlineData("百万", "10010000")]
    [InlineData("あ三い", "あ3い")]      // 表に無い文字は素通し
    [InlineData("5", "5")]
    [InlineData("５", "5")]            // 全角
    [InlineData("日本", "日本")]        // 数字でない漢字は不変(京は数詞なので避ける)
    [InlineData("東京", "東10000000000000000")] // 「京」=10^16 は数詞なので変換される(本家同様)
    public void KanjiNumberToArabicNumber_Cases(string input, string expected)
        => Assert.Equal(expected, NumberUtil.KanjiNumberToArabicNumber(input));

    [Theory]
    [InlineData("123", true)]
    [InlineData("１２３", true)]
    [InlineData("12a", false)]
    [InlineData("", false)]
    public void IsArabicNumber_Cases(string s, bool expected)
        => Assert.Equal(expected, NumberUtil.IsArabicNumber(s));

    [Theory]
    [InlineData("123", true)]
    [InlineData("１２３", false)]   // 全角は ASCII 数字でない
    [InlineData("", false)]
    public void IsDecimalInteger_Cases(string s, bool expected)
        => Assert.Equal(expected, NumberUtil.IsDecimalInteger(s));

    [Fact]
    public void ArabicToVariants_CircledAndRoman()
    {
        var values = new List<string>();
        foreach (NumberUtil.NumberString n in NumberUtil.ArabicToVariants("3"))
        {
            values.Add(n.Value);
        }
        Assert.Contains("③", values);   // 丸数字
        Assert.Contains("Ⅲ", values);   // ローマ数字大
        Assert.Contains("ⅲ", values);   // ローマ数字小
    }

    [Fact]
    public void ArabicToVariants_LargeNumber_NoCircled()
    {
        var values = new List<string>();
        foreach (NumberUtil.NumberString n in NumberUtil.ArabicToVariants("100"))
        {
            values.Add(n.Value);
        }
        Assert.Contains("百", values);
        Assert.DoesNotContain(values, v => v.Length == 1 && v[0] is >= '①' and <= '⑳'); // 丸数字なし
    }

    [Theory]
    [InlineData("百二十", "120")]
    [InlineData("二百十一", "211")]
    [InlineData("百二十万", "1200000")]
    [InlineData("千二百三十四", "1234")]
    [InlineData("一万二千", "12000")]
    [InlineData("十", "10")]
    [InlineData("二十", "20")]
    [InlineData("廿", "20")]      // 廿=20
    [InlineData("五四三", "543")] // スケーリングなし→10進連結
    [InlineData("123", "123")]
    public void TryNormalizeNumber_KanjiToArabic(string input, string expected)
    {
        Assert.True(NumberUtil.TryNormalizeNumber(input, trimLeadingZeros: true, out string arabic));
        Assert.Equal(expected, arabic);
    }

    [Theory]
    [InlineData("あ")]        // 数字でない
    [InlineData("百二百")]    // 不正な並び(base 降順違反)
    [InlineData("")]
    public void TryNormalizeNumber_Invalid(string input)
    {
        Assert.False(NumberUtil.TryNormalizeNumber(input, trimLeadingZeros: true, out _));
    }

    [Fact]
    public void TryNormalizeNumber_KeepsLeadingZeros()
    {
        Assert.True(NumberUtil.TryNormalizeNumber("〇〇五", trimLeadingZeros: false, out string a));
        Assert.Equal("005", a);
    }
}
