using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class A11yDescriptionTests
{
    [Fact]
    public void Describe_Hiragana_LabelsOnceThenChars()
    {
        // "あい" → 本体 + "。 ヒラガナ あ" + "い"
        Assert.Equal("あい。ヒラガナ あい", A11yDescription.Describe("あい"));
    }

    [Fact]
    public void Describe_Katakana()
    {
        Assert.Equal("アイ。カタカナ アイ", A11yDescription.Describe("アイ"));
    }

    [Fact]
    public void Describe_MixedHiraganaKatakana_RelabelsOnSwitch()
    {
        // ひらがな→カタカナに切り替わる所で再ラベル。
        Assert.Equal("あア。ヒラガナ あ。カタカナ ア", A11yDescription.Describe("あア"));
    }

    [Fact]
    public void Describe_HalfWidthSmallKatakana_ExpandsToLarge()
    {
        // 半角小書き "ｧ" → "ハンカクカタカナコモジ ｱ"(大書きへ)
        Assert.Equal("ｧ。ハンカクカタカナコモジ ｱ", A11yDescription.Describe("ｧ"));
    }

    [Fact]
    public void Describe_Alphabet_UpperLower()
    {
        Assert.Equal("Ab。オオモジ A。コモジ b", A11yDescription.Describe("Ab"));
    }

    [Fact]
    public void Describe_OtherChars_Skipped()
    {
        // 漢字や記号は説明なし(本体のみ)。
        Assert.Equal("漢", A11yDescription.Describe("漢"));
    }
}
