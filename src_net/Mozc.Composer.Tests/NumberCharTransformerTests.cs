using Mozc.Composer;
using Xunit;

namespace Mozc.Composer.Tests;

public class NumberCharTransformerTests
{
    [Theory]
    [InlineData("1ー2", "1−2")]    // 数字間のー → −
    [InlineData("0、5", "0，5")]   // 数字後の、 → ，
    [InlineData("3。14", "3．14")] // 数字後の。 → ．
    [InlineData("ー5", "−5")]      // 先頭ーの後が数字 → −
    public void Transform_Converts(string input, string expected)
    {
        (bool changed, string result) = NumberCharTransformer.Transform(input);
        Assert.True(changed);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("あいう")]     // 英数字も記号もない
    [InlineData("ーー")]       // 記号のみ(英数字なし)
    [InlineData("123")]        // 英数字のみ(記号なし)
    [InlineData("あ、い")]     // 、の前が英数字でない
    public void Transform_NoChange(string input)
    {
        (bool changed, string result) = NumberCharTransformer.Transform(input);
        Assert.False(changed);
        Assert.Equal(input, result);
    }
}
