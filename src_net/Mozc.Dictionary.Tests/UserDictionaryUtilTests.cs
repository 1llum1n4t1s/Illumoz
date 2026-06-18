using Mozc.Dictionary;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class UserDictionaryUtilTests
{
    [Theory]
    [InlineData("グーグル", "ぐーぐる")]   // カタカナ→ひらがな
    [InlineData("ｸﾞｰｸﾞﾙ", "ぐーぐる")]    // 半角カナ→全角→ひらがな
    [InlineData("ＡＢＣ", "ABC")]          // 全角ASCII→半角
    [InlineData("あいう", "あいう")]
    public void NormalizeReading_Normalizes(string input, string expected)
    {
        Assert.Equal(expected, UserDictionaryUtil.NormalizeReading(input));
    }

    [Theory]
    [InlineData("ぐーぐる", true)]
    [InlineData("google", true)]    // ASCII 可
    [InlineData("漢字", false)]      // 漢字は読みとして不可
    [InlineData("グーグル", true)]   // 正規化でひらがなに
    public void IsValidReading_ChecksCharset(string reading, bool expected)
    {
        Assert.Equal(expected, UserDictionaryUtil.IsValidReading(reading));
    }

    [Fact]
    public void ContainsInvalidChars_DetectsControlChars()
    {
        Assert.True(UserDictionaryUtil.ContainsInvalidChars("a\tb"));
        Assert.True(UserDictionaryUtil.ContainsInvalidChars("a\nb"));
        Assert.False(UserDictionaryUtil.ContainsInvalidChars("ab"));
    }

    [Fact]
    public void IsTooLongString_ChecksByteLength()
    {
        Assert.False(UserDictionaryUtil.IsTooLongString(new string('a', 300)));
        Assert.True(UserDictionaryUtil.IsTooLongString(new string('a', 301)));
        Assert.True(UserDictionaryUtil.IsTooLongString(new string('あ', 101))); // 3B*101=303
    }

    [Theory]
    [InlineData("ぐーぐる", "Google", "", UserDictionaryUtil.ValidationResult.Ok)]
    [InlineData("", "Google", "", UserDictionaryUtil.ValidationResult.ReadingEmpty)]
    [InlineData("ぐーぐる", "", "", UserDictionaryUtil.ValidationResult.WordEmpty)]
    [InlineData("ぐ\tる", "Google", "", UserDictionaryUtil.ValidationResult.ReadingContainsInvalidChar)]
    [InlineData("ぐーぐる", "Goo\ngle", "", UserDictionaryUtil.ValidationResult.WordContainsInvalidChar)]
    [InlineData("ぐーぐる", "Google", "co\tmment", UserDictionaryUtil.ValidationResult.CommentContainsInvalidChar)]
    public void ValidateEntry_ReturnsExpected(string r, string w, string c, UserDictionaryUtil.ValidationResult expected)
    {
        Assert.Equal(expected, UserDictionaryUtil.ValidateEntry(r, w, c));
    }
}
