using System.Text;
using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class CharacterFormManagerTests
{
    [Fact]
    public void ConvertWidth_FullAndHalf()
    {
        Assert.Equal("０", CharacterFormManager.ConvertWidth("0", CharacterForm.FullWidth));
        Assert.Equal("0", CharacterFormManager.ConvertWidth("０", CharacterForm.HalfWidth));
        Assert.Equal("0", CharacterFormManager.ConvertWidth("0", CharacterForm.NoConversion));
    }

    [Fact]
    public void GetCharacterForm_UsesRuleGroup()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.HalfWidth);   // 数字グループ
        m.AddRule("ア", CharacterForm.FullWidth);   // カタカナグループ
        Assert.Equal(CharacterForm.HalfWidth, m.GetCharacterForm("5"));   // 別の数字でも同じ
        Assert.Equal(CharacterForm.HalfWidth, m.GetCharacterForm("９"));  // 全角数字も同じグループ
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("ン"));  // 別のカタカナ
        Assert.Equal(CharacterForm.NoConversion, m.GetCharacterForm("あ")); // 規則なし
    }

    [Fact]
    public void ConvertString_NumbersToHalfWidth()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.HalfWidth);
        Assert.Equal("123", m.ConvertString("１２３"));
    }

    [Fact]
    public void ConvertString_KatakanaToFullWidth()
    {
        var m = new CharacterFormManager();
        m.AddRule("ア", CharacterForm.FullWidth);
        Assert.Equal("カタカナ", m.ConvertString("ｶﾀｶﾅ"));
    }

    [Fact]
    public void ConvertString_HiraganaKanji_Unchanged()
    {
        var m = CharacterFormManager.CreatePreeditDefault();
        Assert.Equal("漢字あいう", m.ConvertString("漢字あいう"));
    }

    [Fact]
    public void ConvertString_MixedRuns()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.HalfWidth);   // 数字は半角
        m.AddRule("ア", CharacterForm.FullWidth);   // カタカナは全角
        // 全角数字→半角、半角カナ→全角、ひらがなはそのまま
        Assert.Equal("123カタカナあ", m.ConvertString("１２３ｶﾀｶﾅあ"));
    }
}
