using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

// mozc.data の "dict" セクション相当(DictionaryFileCodec 梱包)の pack/unpack 往復。
public class SystemDictionaryImageTests
{
    [Fact]
    public void BuildDictionaryImage_OpenFromImage_RoundTrips()
    {
        var tokens = new[]
        {
            new Token("とうきょう", "東京", 1234, 100, 200),
            new Token("おおさか", "大阪", 2000, 100, 200),
            new Token("あめ", "雨", 500, 10, 10),
            new Token("あめ", "飴", 600, 20, 20),
        };

        byte[] dictImage = new SystemDictionaryBuilder().BuildDictionaryImage(tokens);
        SystemDictionary dict = SystemDictionary.OpenFromDictionaryImage(dictImage);

        var values = new List<string>();
        dict.LookupExact("あめ", new InlineDictionaryCallback
        {
            TokenHandler = (_, _, t) => { values.Add(t.Value); return DictionaryCallback.ResultType.TraverseContinue; },
        });
        Assert.Contains("雨", values);
        Assert.Contains("飴", values);

        Assert.True(dict.HasKey("とうきょう"));
        Assert.True(dict.HasValue("大阪"));
        Assert.False(dict.HasKey("きょうと"));

        var tokyo = new List<string>();
        dict.LookupExact("とうきょう", new InlineDictionaryCallback
        {
            TokenHandler = (_, _, t) => { tokyo.Add(t.Value); return DictionaryCallback.ResultType.TraverseContinue; },
        });
        Assert.Equal(new[] { "東京" }, tokyo);
    }
}
