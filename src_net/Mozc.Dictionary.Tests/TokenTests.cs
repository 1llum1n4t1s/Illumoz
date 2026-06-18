using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class TokenTests
{
    [Fact]
    public void Token_AttributeFlags_MatchCxx()
    {
        Assert.Equal(0, (int)Token.Attribute.None);
        Assert.Equal(1, (int)Token.Attribute.SpellingCorrection);
        Assert.Equal(0x40, (int)Token.Attribute.SuffixDictionary);
        Assert.Equal(0x80, (int)Token.Attribute.UserDictionary);
    }

    [Fact]
    public void TokenInfo_Defaults_MatchCxxClear()
    {
        var t = new Token("よみ", "読み", 100, 1, 2);
        var info = new TokenInfo(t);
        Assert.Same(t, info.Token);
        Assert.Equal(-1, info.IdInValueTrie);
        Assert.Equal(-1, info.IdInFrequentPosMap);
        Assert.Equal(TokenInfo.PosType.DefaultPos, info.Pos);
        Assert.Equal(TokenInfo.ValueType.DefaultValue, info.Value);
        Assert.Equal(TokenInfo.CostType.DefaultCost, info.Cost);
        Assert.Equal(TokenInfo.AccentEncodingType.EncodedInValue, info.AccentEncoding);
        Assert.Equal(-1, info.AccentType);
    }
}
