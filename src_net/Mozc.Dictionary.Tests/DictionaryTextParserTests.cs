using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class DictionaryTextParserTests
{
    [Fact]
    public void ParseLine_TabSeparated()
    {
        Token? t = DictionaryTextParser.ParseLine("ああると\t1851\t1851\t7129\tアアルト");
        Assert.NotNull(t);
        Assert.Equal("ああると", t!.Key);
        Assert.Equal("アアルト", t.Value);
        Assert.Equal(1851, t.Lid);
        Assert.Equal(1851, t.Rid);
        Assert.Equal(7129, t.Cost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("incomplete\t1\t2")]
    public void ParseLine_InvalidReturnsNull(string line)
        => Assert.Null(DictionaryTextParser.ParseLine(line));

    [Fact]
    public void ParseLine_NonNumericThrows()
        => Assert.Throws<FormatException>(() => DictionaryTextParser.ParseLine("k\tx\t2\t3\tv"));

    [Fact]
    public void Parse_ThenBuildSystemDictionary_RoundTrips()
    {
        // C6→C5 結線: テキスト→Token→SystemDictionaryBuilder→reader で引ける。
        string[] lines =
        {
            "とうきょう\t100\t100\t1234\t東京",
            "おおさか\t100\t100\t2000\t大阪",
            "", // 空行は無視
            "あめ\t10\t10\t500\t雨",
        };
        List<Token> tokens = DictionaryTextParser.Parse(lines);
        Assert.Equal(3, tokens.Count);

        var dict = new SystemDictionaryBuilder().Build(tokens);
        var got = new List<string>();
        dict.LookupExact("とうきょう", new InlineDictionaryCallback
        {
            TokenHandler = (_, _, token) => { got.Add(token.Value); return DictionaryCallback.ResultType.TraverseContinue; },
        });
        Assert.Equal(new[] { "東京" }, got);
        Assert.True(dict.HasKey("おおさか"));
        Assert.True(dict.HasValue("雨"));
    }
}
