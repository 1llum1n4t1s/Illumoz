using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class UnicodeRewriterTests
{
    private static Segments OneSegment(string key, int extraCandidates = 0)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        for (int i = 0; i <= extraCandidates; i++)
        {
            Candidate c = seg.AddCandidate();
            c.Key = key;
            c.Value = key + i;
            c.ContentKey = key;
            c.ContentValue = key + i;
        }
        return segments;
    }

    [Theory]
    [InlineData("U+3042", "あ")]
    [InlineData("U+0041", "A")]
    [InlineData("U+1F600", "\U0001F600")] // 😀 (サロゲートペア)
    public void Convert_Valid(string key, string expected)
    {
        Assert.True(UnicodeRewriter.TryConvert(key, out string value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("3042")]      // 接頭辞なし
    [InlineData("U+")]        // 桁なし
    [InlineData("U+ZZZZ")]    // 非16進
    [InlineData("U+D800")]    // サロゲート
    [InlineData("U+0007")]    // 制御文字
    [InlineData("U+FFFE")]    // 非文字
    [InlineData("U+110000")]  // 範囲外
    public void Convert_Invalid(string key)
    {
        Assert.False(UnicodeRewriter.TryConvert(key, out _));
    }

    [Fact]
    public void Rewrite_InsertsCharacterCandidate()
    {
        var segments = OneSegment("U+3042");
        var rw = new UnicodeRewriter();
        Assert.True(rw.Rewrite(segments));

        Segment seg = segments.ConversionSegment(0);
        bool found = false;
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            if (seg.Get(i).Value == "あ")
            {
                found = true;
                Assert.Equal("Unicode 変換 (U+3042)", seg.Get(i).Description);
            }
        }
        Assert.True(found);
    }

    [Theory]
    [InlineData("A", "U+0041")]
    [InlineData("あ", "U+3042")]
    [InlineData("\U0001F600", "U+1F600")] // 😀 サロゲートペア
    public void ToUnicodeFormat_SingleChar(string text, string expected)
    {
        Assert.Equal(expected, UnicodeRewriter.ToUnicodeFormat(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]   // 2文字
    [InlineData("あい")] // 2文字
    public void ToUnicodeFormat_NotSingle_ReturnsNull(string text)
    {
        Assert.Null(UnicodeRewriter.ToUnicodeFormat(text));
    }

    [Fact]
    public void Rewrite_NonUnicodeKey_NoChange()
    {
        var segments = OneSegment("あした");
        var rw = new UnicodeRewriter();
        Assert.False(rw.Rewrite(segments));
    }
}
