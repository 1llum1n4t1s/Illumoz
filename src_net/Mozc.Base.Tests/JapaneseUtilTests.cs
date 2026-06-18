using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class JapaneseUtilTests
{
    [Theory]
    [InlineData("あいうえお", "アイウエオ")]
    [InlineData("がぎぐ", "ガギグ")]
    [InlineData("ゔ", "ヴ")]
    [InlineData("ぁぃぅ", "ァィゥ")]
    public void HiraganaToKatakana_Roundtrip(string hira, string kata)
    {
        Assert.Equal(kata, JapaneseUtil.HiraganaToKatakana(hira));
        Assert.Equal(hira, JapaneseUtil.KatakanaToHiragana(kata));
    }

    [Theory]
    [InlineData("abcABC123", "ａｂｃＡＢＣ１２３")]
    [InlineData("!@#", "！＠＃")]
    public void Ascii_WidthConversion(string half, string full)
    {
        Assert.Equal(full, JapaneseUtil.HalfWidthAsciiToFullWidthAscii(half));
        Assert.Equal(half, JapaneseUtil.FullWidthAsciiToHalfWidthAscii(full));
    }

    [Fact]
    public void HalfWidthKatakana_ToFullWidth()
    {
        // 半角カナ ｱｲｳ → 全角 アイウ
        Assert.Equal("アイウ", JapaneseUtil.HalfWidthKatakanaToFullWidthKatakana("ｱｲｳ"));
        // 濁点付き半角 ｶﾞ → ガ
        Assert.Equal("ガ", JapaneseUtil.HalfWidthKatakanaToFullWidthKatakana("ｶﾞ"));
    }

    [Fact]
    public void FullWidthToHalfWidth_Composite()
    {
        Assert.Equal("abcｱｲｳ", JapaneseUtil.FullWidthToHalfWidth("ａｂｃアイウ"));
    }

    [Fact]
    public void NonTargetCharsArePreserved()
    {
        Assert.Equal("漢字あ", JapaneseUtil.FullWidthAsciiToHalfWidthAscii("漢字あ"));
    }
}
