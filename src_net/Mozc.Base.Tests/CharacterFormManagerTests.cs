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
    public void FromRules_BuildsTable()
    {
        var m = CharacterFormManager.FromRules(new[]
        {
            ("0", CharacterForm.HalfWidth),
            ("A", CharacterForm.FullWidth),
            ("", CharacterForm.HalfWidth), // 空 group は無視
        });
        Assert.Equal(CharacterForm.HalfWidth, m.GetCharacterForm("7"));
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("x"));
        Assert.Equal("0", m.ConvertString("０"));
        Assert.Equal("Ａ", m.ConvertString("A"));
    }

    [Fact]
    public void LastForm_DefaultsToFullWidth()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.LastForm);
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("5"));
    }

    [Fact]
    public void LastForm_RemembersChosenForm()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.LastForm);
        m.SetCharacterForm("0", CharacterForm.HalfWidth);
        Assert.Equal(CharacterForm.HalfWidth, m.GetCharacterForm("9"));
        Assert.Equal("123", m.ConvertString("１２３")); // 記憶した半角で整形

        m.ClearHistory();
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("9")); // 既定に戻る
    }

    [Fact]
    public void GuessAndSetCharacterForm_LearnsFromWidth()
    {
        var m = new CharacterFormManager();
        m.AddRule("A", CharacterForm.LastForm);
        m.GuessAndSetCharacterForm("ＡＢＣ"); // 全角を観測
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("x"));
        m.GuessAndSetCharacterForm("abc");    // 半角を観測
        Assert.Equal(CharacterForm.HalfWidth, m.GetCharacterForm("x"));
    }

    [Fact]
    public void SetCharacterForm_IgnoredForNonLastFormRule()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.FullWidth); // 固定ルール
        m.SetCharacterForm("0", CharacterForm.HalfWidth); // 無視される
        Assert.Equal(CharacterForm.FullWidth, m.GetCharacterForm("0"));
    }

    [Fact]
    public void History_SerializeRoundTrip()
    {
        var m = new CharacterFormManager();
        m.AddRule("0", CharacterForm.LastForm);
        m.AddRule("A", CharacterForm.LastForm);
        m.SetCharacterForm("0", CharacterForm.HalfWidth);
        m.SetCharacterForm("A", CharacterForm.FullWidth);
        byte[] bytes = m.SerializeHistory();

        var m2 = new CharacterFormManager();
        m2.AddRule("0", CharacterForm.LastForm);
        m2.AddRule("A", CharacterForm.LastForm);
        Assert.True(m2.DeserializeHistory(bytes));
        Assert.Equal(CharacterForm.HalfWidth, m2.GetCharacterForm("9"));
        Assert.Equal(CharacterForm.FullWidth, m2.GetCharacterForm("z"));
    }

    [Fact]
    public void History_DeserializeRejectsBadMagic()
    {
        var m = new CharacterFormManager();
        Assert.False(m.DeserializeHistory(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }));
    }

    [Fact]
    public void History_SaveLoadFile()
    {
        string guid = global::System.Guid.NewGuid().ToString("N");
        string path = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), $"mzcf_{guid}.bin");
        try
        {
            var m = new CharacterFormManager();
            m.AddRule("0", CharacterForm.LastForm);
            m.SetCharacterForm("0", CharacterForm.HalfWidth);
            m.SaveHistory(path);

            var m2 = new CharacterFormManager();
            m2.AddRule("0", CharacterForm.LastForm);
            Assert.True(m2.LoadHistory(path));
            Assert.Equal(CharacterForm.HalfWidth, m2.GetCharacterForm("3"));
        }
        finally
        {
            if (global::System.IO.File.Exists(path))
            {
                global::System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void History_LoadMissingFile_ReturnsFalse()
    {
        var m = new CharacterFormManager();
        Assert.False(m.LoadHistory(global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), "no_such_mzcf_file.bin")));
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
