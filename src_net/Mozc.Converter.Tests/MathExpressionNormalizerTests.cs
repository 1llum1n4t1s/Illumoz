using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class MathExpressionNormalizerTests
{
    [Theory]
    [InlineData("1+2", "1+2")]
    [InlineData("１＋２", "1+2")]      // 全角
    [InlineData("3×4", "3*4")]        // × → *
    [InlineData("6÷2", "6/2")]        // ÷ → /
    [InlineData("1・2", "1/2")]       // ・ → /
    [InlineData("5ー3", "5-3")]       // 長音 → -
    [InlineData("（１＝２）", "(1=2)")]
    public void TryNormalize_MathExpressions(string input, string expected)
    {
        Assert.True(MathExpressionNormalizer.TryNormalize(input, out string key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("あ")]       // ひらがな
    [InlineData("1+あ")]     // 数式外文字混在
    [InlineData("漢字")]
    public void TryNormalize_NonMath_ReturnsFalse(string input)
    {
        Assert.False(MathExpressionNormalizer.TryNormalize(input, out _));
    }

    [Fact]
    public void TryNormalize_Empty_ReturnsTrueEmpty()
    {
        Assert.True(MathExpressionNormalizer.TryNormalize("", out string key));
        Assert.Equal("", key);
    }
}
