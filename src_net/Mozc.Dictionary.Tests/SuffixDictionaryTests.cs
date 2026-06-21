using System.Collections.Generic;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class SuffixDictionaryTests
{
    private static SuffixDictionary Build() => new(new[]
    {
        new SuffixDictionary.Entry("です", "です", 1, 1, 100),
        new SuffixDictionary.Entry("ですか", "ですか", 1, 1, 110),
        new SuffixDictionary.Entry("ます", "ます", 2, 2, 100),
        new SuffixDictionary.Entry("さん", "さん", 3, 3, 100),
    });

    private static List<Token> Collect(SuffixDictionary d, string key)
    {
        var tokens = new List<Token>();
        d.LookupPredictive(key, new InlineDictionaryCallback
        {
            TokenHandler = (k, ek, t) =>
            {
                tokens.Add(t);
                return DictionaryCallback.ResultType.TraverseContinue;
            },
        });
        return tokens;
    }

    [Fact]
    public void LookupPredictive_PrefixMatch()
    {
        List<Token> tokens = Collect(Build(), "です");
        var values = new List<string>();
        foreach (Token t in tokens)
        {
            values.Add(t.Value);
        }
        Assert.Equal(2, tokens.Count);          // です, ですか
        Assert.Contains("です", values);
        Assert.Contains("ですか", values);
        Assert.DoesNotContain("ます", values);
        Assert.All(tokens, t => Assert.Equal(Token.Attribute.SuffixDictionary, t.Attributes));
    }

    [Fact]
    public void LookupPredictive_NoMatch_Empty()
    {
        Assert.Empty(Collect(Build(), "ほげ"));
    }

    [Fact]
    public void LookupPredictive_CarriesLidRidCost()
    {
        List<Token> tokens = Collect(Build(), "ます");
        Assert.Single(tokens);
        Assert.Equal(2, tokens[0].Lid);
        Assert.Equal(2, tokens[0].Rid);
        Assert.Equal(100, tokens[0].Cost);
    }

    [Fact]
    public void LookupPredictive_TraverseDone_Stops()
    {
        var d = Build();
        int count = 0;
        d.LookupPredictive("です", new InlineDictionaryCallback
        {
            KeyHandler = _ =>
            {
                count++;
                return DictionaryCallback.ResultType.TraverseDone;
            },
        });
        Assert.Equal(1, count); // 最初の OnKey で終了
    }

    [Fact]
    public void LookupPredictive_EmptyValueFallsBackToKey()
    {
        var d = new SuffixDictionary(new[]
        {
            new SuffixDictionary.Entry("ね", "", 1, 1, 50),
        });
        List<Token> tokens = Collect(d, "ね");
        Assert.Single(tokens);
        Assert.Equal("ね", tokens[0].Value); // value 空なら key を使う
    }
}
