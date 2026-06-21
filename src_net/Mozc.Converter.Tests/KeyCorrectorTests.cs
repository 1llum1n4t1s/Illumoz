using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class KeyCorrectorTests
{
    [Theory]
    // RewriteNN: ん[あいうえお]→ん[なにぬねの](先頭以外なので前に文字を置く)
    [InlineData("かんあ", "かんな")]
    [InlineData("かんお", "かんの")]
    // RewriteNI: に[ゃゅょ]→ん[やゆよ]
    [InlineData("にゃ", "んや")]
    [InlineData("にょ", "んよ")]
    // RewriteM: m[ば..ぽ]→ん[..](先頭以外)
    [InlineData("amば", "aんば")]
    [InlineData("aｍぽ", "aんぽ")]
    // RewriteSmallTSU: [^っ]っっ[^っ]→[^っ]っ[^っ]
    [InlineData("きっって", "きって")]
    // RewriteYu: [きしちにひり]ゅ[^う]→...ゅう(挿入)
    [InlineData("きゅた", "きゅうた")]
    // RewriteDoubleNN: [^ん]んん[^あいうえお]→[^ん]ん[...]
    [InlineData("かんんと", "かんと")]
    public void CorrectReading_Corrects(string input, string expected)
    {
        Assert.Equal(expected, KeyCorrector.CorrectReading(input));
    }

    [Theory]
    [InlineData("あいう")]      // 補正対象なし
    [InlineData("んあ")]        // 先頭の "ん" は補正しない(key_pos==0)
    [InlineData("mば")]         // 先頭の "m" は補正しない
    [InlineData("きゅう")]      // 既に "う" 付きなら不変
    public void CorrectReading_NoChange(string input)
    {
        Assert.Equal(input, KeyCorrector.CorrectReading(input));
    }

    [Fact]
    public void CorrectReading_DoubleNN_BeforeVowel_LeavesNNVowel()
    {
        // "かんんあ" → 先頭"か"を出し "んあ" を残す → RewriteNN で "んな"
        Assert.Equal("かんな", KeyCorrector.CorrectReading("かんんあ"));
    }

    [Fact]
    public void CorrectReading_Empty()
    {
        Assert.Equal("", KeyCorrector.CorrectReading(""));
    }
}
