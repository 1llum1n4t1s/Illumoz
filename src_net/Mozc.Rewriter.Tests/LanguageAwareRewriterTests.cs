using System;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class LanguageAwareRewriterTests
{
    private static readonly Func<string, bool> NoKey = _ => false;
    private static readonly Func<string, bool> NoValue = _ => false;

    [Fact]
    public void IsRawQuery_TooShort_False()
    {
        Assert.False(LanguageAwareRewriter.IsRawQuery("cat", "ｃａｔ", "ｃａｔ", NoKey, NoValue, out _));
    }

    [Fact]
    public void IsRawQuery_CompositionEqualsRaw_False()
    {
        Assert.False(LanguageAwareRewriter.IsRawQuery("google", "google", "google", NoKey, NoValue, out _));
    }

    [Fact]
    public void IsRawQuery_FullWidthOfRaw_False()
    {
        // 表示が打鍵の全角形 → 追加不要。
        Assert.False(LanguageAwareRewriter.IsRawQuery("google", "ｇｏｏｇｌｅ", "ｇｏｏｇｌｅ", NoKey, NoValue, out _));
    }

    [Fact]
    public void IsRawQuery_AlphabetInMiddle_True()
    {
        // 予測キーの途中に英字 → rank 0 で raw query。
        Assert.True(LanguageAwareRewriter.IsRawQuery("example", "えぁｍｐぇ", "えぁｍｐぇ", NoKey, NoValue, out int rank));
        Assert.Equal(0, rank);
    }

    [Fact]
    public void IsRawQuery_KeyInDictionary_False()
    {
        // 予測キーが辞書キー(はな) → raw 扱いしない。
        Assert.False(LanguageAwareRewriter.IsRawQuery("hana", "はな", "はな", k => k == "はな", NoValue, out _));
    }

    [Fact]
    public void IsRawQuery_RawTextIsDictionaryValue_True()
    {
        // 打鍵が辞書の値(remove) → rank 2 で raw query。
        Assert.True(LanguageAwareRewriter.IsRawQuery(
            "remove", "れもヴぇ", "れもヴぇ", NoKey, v => v == "remove", out int rank));
        Assert.Equal(2, rank);
    }
}
