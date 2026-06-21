using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

// codec.cc DecodeToken をブランチ別の手組みバイト列(仕様から手計算した正解)で検証。
// エンコーダに依存しない ground truth テスト。
public class SystemDictionaryCodecTokenTests
{
    private readonly SystemDictionaryCodec _codec = new();

    private TokenInfo Decode(byte[] bytes, out bool hasNext, out int read)
    {
        var info = new TokenInfo(new Token());
        hasNext = _codec.DecodeToken(bytes, info, out read);
        return info;
    }

    [Fact]
    public void FullPos_LargeCost_NormalValue()
    {
        // flag=0x04(full pos,normal value). lid=0x123,rid=0x45,cost=0x1234,id=0x010203
        byte[] b = { 0x04, 0x23, 0x51, 0x04, 0x12, 0x34, 0x03, 0x02, 0x01 };
        var info = Decode(b, out bool hasNext, out int read);
        Assert.True(hasNext);
        Assert.Equal(9, read);
        Assert.Equal(0x123, info.Token.Lid);
        Assert.Equal(0x45, info.Token.Rid);
        Assert.Equal(0x1234, info.Token.Cost);
        Assert.Equal(TokenInfo.ValueType.DefaultValue, info.Value);
        Assert.Equal(0x010203, info.IdInValueTrie);
        Assert.Equal(Token.Attribute.None, info.Token.Attributes);
    }

    [Fact]
    public void FrequentPos_SmallCost_AsIsHiragana_LastToken()
    {
        // flag=0x81(frequent pos,as-is hiragana,last). pos_id=7,cost=0x0500
        byte[] b = { 0x81, 0x07, 0x85 };
        var info = Decode(b, out bool hasNext, out int read);
        Assert.False(hasNext);              // last token
        Assert.Equal(3, read);
        Assert.Equal(TokenInfo.PosType.FrequentPos, info.Pos);
        Assert.Equal(7, info.IdInFrequentPosMap);
        Assert.Equal(0x0500, info.Token.Cost);
        Assert.Equal(TokenInfo.ValueType.AsIsHiragana, info.Value);
    }

    [Fact]
    public void CrammedId_FrequentPos_SmallCost()
    {
        // flag=0x52(crammed, low6=0x12=id上位). pos_id=9,cost=0x0300,id=0x123456
        byte[] b = { 0x52, 0x09, 0x83, 0x56, 0x34 };
        var info = Decode(b, out bool hasNext, out int read);
        Assert.True(hasNext);
        Assert.Equal(5, read);
        Assert.Equal(TokenInfo.PosType.FrequentPos, info.Pos);
        Assert.Equal(9, info.IdInFrequentPosMap);
        Assert.Equal(0x0300, info.Token.Cost);
        Assert.Equal(0x123456, info.IdInValueTrie);
    }

    [Fact]
    public void SpellingCorrection_MonoPos_SameAsPrevValue_LargeCost()
    {
        // flag=0x1b(mono pos,same-as-prev value,spelling). lid=rid=0x102,cost=0x1234
        byte[] b = { 0x1b, 0x02, 0x01, 0x12, 0x34 };
        var info = Decode(b, out bool hasNext, out int read);
        Assert.True(hasNext);
        Assert.Equal(5, read);
        Assert.Equal(Token.Attribute.SpellingCorrection, info.Token.Attributes);
        Assert.Equal(0x102, info.Token.Lid);
        Assert.Equal(0x102, info.Token.Rid);
        Assert.Equal(0x1234, info.Token.Cost);
        Assert.Equal(TokenInfo.ValueType.SameAsPrevValue, info.Value);
    }

    [Fact]
    public void SameAsPrevPos_AsIsKatakana_SmallCost_LastToken()
    {
        // flag=0x8e(same-as-prev pos,as-is katakana,last). cost=0x0700
        byte[] b = { 0x8e, 0x87 };
        var info = Decode(b, out bool hasNext, out int read);
        Assert.False(hasNext);
        Assert.Equal(2, read);
        Assert.Equal(TokenInfo.PosType.SameAsPrevPos, info.Pos);
        Assert.Equal(0x0700, info.Token.Cost);
        Assert.Equal(TokenInfo.ValueType.AsIsKatakana, info.Value);
    }

    [Fact]
    public void SectionNames_AndTerminationFlag()
    {
        Assert.Equal("k", _codec.SectionNameForKey);
        Assert.Equal("v", _codec.SectionNameForValue);
        Assert.Equal("t", _codec.SectionNameForTokens);
        Assert.Equal("p", _codec.SectionNameForPos);
        Assert.Equal(0xff, _codec.GetTokensTerminationFlag());
    }
}
