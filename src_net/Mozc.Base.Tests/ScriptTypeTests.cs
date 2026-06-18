using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class ScriptTypeTests
{
    [Theory]
    [InlineData("あいう", ScriptType.Hiragana)]
    [InlineData("アイウ", ScriptType.Katakana)]
    [InlineData("東京", ScriptType.Kanji)]
    [InlineData("123", ScriptType.Numeric)]
    [InlineData("Google", ScriptType.Alphabet)]
    [InlineData("あア", ScriptType.Unknown)]     // 混在
    [InlineData("カー", ScriptType.Katakana)]     // 長音はかな扱い
    [InlineData("はー", ScriptType.Hiragana)]
    [InlineData("ー", ScriptType.Unknown)]        // 長音のみ→かな2種で曖昧
    [InlineData("1.5", ScriptType.Numeric)]        // 2文字目以降の . は数字
    [InlineData(".5", ScriptType.Unknown)]         // 先頭の . は数字扱いしない
    [InlineData("", ScriptType.Unknown)]
    public void GetScriptType_Cases(string s, ScriptType expected)
        => Assert.Equal(expected, ScriptClassifier.GetScriptType(s));

    [Theory]
    [InlineData("東京!", ScriptType.Kanji)]   // 記号を無視→漢字
    [InlineData("あ。", ScriptType.Hiragana)]
    public void GetScriptTypeWithoutSymbols_IgnoresSymbols(string s, ScriptType expected)
        => Assert.Equal(expected, ScriptClassifier.GetScriptTypeWithoutSymbols(s));

    [Theory]
    [InlineData("Google", true)]
    [InlineData("Hello World", true)]
    [InlineData("don't-stop", true)]
    [InlineData("Wow!", true)]
    [InlineData("グーグル", false)]
    [InlineData("abc123", false)]
    public void IsEnglishTransliteration_Cases(string s, bool expected)
        => Assert.Equal(expected, ScriptClassifier.IsEnglishTransliteration(s));
}
