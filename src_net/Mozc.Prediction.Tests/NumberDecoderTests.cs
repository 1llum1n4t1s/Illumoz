using Mozc.Prediction;
using Xunit;

namespace Mozc.Prediction.Tests;

public class NumberDecoderTests
{
    [Theory]
    [InlineData("ぜろ", "0")]
    [InlineData("いち", "1")]
    [InlineData("じゅう", "10")]
    [InlineData("にじゅう", "20")]
    [InlineData("ひゃく", "100")]
    [InlineData("にひゃく", "200")]
    [InlineData("せん", "1000")]
    [InlineData("せんにひゃく", "1200")]
    [InlineData("にじゅうさん", "23")]
    [InlineData("ひゃくにじゅうさん", "123")]
    [InlineData("きゅうせんきゅうひゃくきゅうじゅうきゅう", "9999")]
    public void DecodeToString_SmallNumbers(string reading, string expected)
    {
        Assert.Equal(expected, new NumberDecoder().DecodeToString(reading));
    }

    [Theory]
    [InlineData("いちまん", "1万")]
    [InlineData("いちまんにせん", "1万2000")]
    [InlineData("いちおく", "1億")]
    public void DecodeToString_BigDigits(string reading, string expected)
    {
        Assert.Equal(expected, new NumberDecoder().DecodeToString(reading));
    }

    [Fact]
    public void DecodeToString_NonNumber_ReturnsNull()
    {
        Assert.Null(new NumberDecoder().DecodeToString("こんにちは"));
    }

    [Fact]
    public void Decode_PartialConsumption_ProducesPrefixResult()
    {
        // "にじゅうにち"(20日): "にち" は接尾語(STOP_DECODING)なので 20 まで。
        string? s = new NumberDecoder().DecodeToString("にじゅうにち");
        Assert.Equal("20", s);
    }

    [Fact]
    public void Decode_InvalidUnitSequence_NotFullyDecoded()
    {
        // "いちさん" は Unit 連続で無効 → "1" のみ。
        Assert.Equal("1", new NumberDecoder().DecodeToString("いちさん"));
    }
}
