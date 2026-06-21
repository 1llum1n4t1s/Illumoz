using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

// codec.cc EncodeTokens のゴールデン/ラウンドトリップ検証。
// (1) DecodeToken テストで手計算した正解バイト列をエンコーダが再現するか(ゴールデン)。
// (2) 複数トークン列を Encode→逐次 Decode して等価に戻るか(ラウンドトリップ)。
public class SystemDictionaryCodecEncodeTokensTests
{
    private readonly SystemDictionaryCodec _codec = new();

    private static TokenInfo MakeToken(
        ushort lid, ushort rid, int cost, int valueTrieId,
        TokenInfo.PosType pos = TokenInfo.PosType.DefaultPos,
        TokenInfo.ValueType value = TokenInfo.ValueType.DefaultValue,
        TokenInfo.CostType costType = TokenInfo.CostType.DefaultCost,
        int frequentPosId = -1,
        Token.Attribute attr = Token.Attribute.None)
    {
        var token = new Token { Lid = lid, Rid = rid, Cost = cost, Attributes = attr };
        return new TokenInfo(token)
        {
            Pos = pos,
            Value = value,
            Cost = costType,
            IdInValueTrie = valueTrieId,
            IdInFrequentPosMap = frequentPosId,
        };
    }

    [Fact]
    public void Encode_FullPos_LargeCost_NormalValue_MatchesGolden()
    {
        // DecodeToken テストと同じ正解: lid=0x123,rid=0x45,cost=0x1234,id=0x010203,非last。
        var tokens = new List<TokenInfo>
        {
            MakeToken(0x123, 0x45, 0x1234, 0x010203),
            MakeToken(0x10, 0x10, 0x0100, 0, pos: TokenInfo.PosType.FrequentPos, frequentPosId: 1,
                      value: TokenInfo.ValueType.AsIsHiragana), // last
        };
        byte[] enc = _codec.EncodeTokens(tokens);
        Assert.Equal(new byte[] { 0x04, 0x23, 0x51, 0x04, 0x12, 0x34, 0x03, 0x02, 0x01 }, enc[..9]);
    }

    [Fact]
    public void Encode_CrammedId_FrequentPos_SmallCost_MatchesGolden()
    {
        // flag=0x52(cram, low6=0x12), pos_id=9, cost small 0x0300, id=0x123456, 非last。
        var tokens = new List<TokenInfo>
        {
            MakeToken(0, 0, 0x0300, 0x123456, pos: TokenInfo.PosType.FrequentPos,
                      costType: TokenInfo.CostType.CanUseSmallEncoding, frequentPosId: 9),
            MakeToken(0x10, 0x10, 0x0100, 0, pos: TokenInfo.PosType.FrequentPos, frequentPosId: 0,
                      value: TokenInfo.ValueType.AsIsKatakana), // last
        };
        byte[] enc = _codec.EncodeTokens(tokens);
        Assert.Equal(new byte[] { 0x52, 0x09, 0x83, 0x56, 0x34 }, enc[..5]);
    }

    [Theory]
    [InlineData(0x123, 0x45, 0x1234, 0x010203, TokenInfo.PosType.DefaultPos, TokenInfo.ValueType.DefaultValue, TokenInfo.CostType.DefaultCost, -1)]
    [InlineData(0x102, 0x102, 0x0234, 0x000007, TokenInfo.PosType.DefaultPos, TokenInfo.ValueType.DefaultValue, TokenInfo.CostType.DefaultCost, -1)]
    [InlineData(0, 0, 0x0500, 0, TokenInfo.PosType.FrequentPos, TokenInfo.ValueType.AsIsHiragana, TokenInfo.CostType.CanUseSmallEncoding, 7)]
    [InlineData(0, 0, 0x0300, 0x1f3456, TokenInfo.PosType.FrequentPos, TokenInfo.ValueType.DefaultValue, TokenInfo.CostType.CanUseSmallEncoding, 9)]
    public void RoundTrip_SingleToken(ushort lid, ushort rid, int cost, int valueId,
        TokenInfo.PosType pos, TokenInfo.ValueType value, TokenInfo.CostType costType, int freqPosId)
    {
        // 単体トークン(=last)を encode→decode して等価に戻ることを確認。
        var src = MakeToken(lid, rid, cost, valueId, pos, value, costType, freqPosId);
        byte[] enc = _codec.EncodeTokens(new[] { src });

        var dec = new TokenInfo(new Token());
        bool hasNext = _codec.DecodeToken(enc, dec, out int read);

        Assert.False(hasNext);        // 単体なので last
        Assert.Equal(enc.Length, read);
        Assert.Equal(pos, dec.Pos);
        Assert.Equal(value, dec.Value);
        Assert.Equal(cost, dec.Token.Cost);

        if (pos == TokenInfo.PosType.FrequentPos)
        {
            Assert.Equal(freqPosId, dec.IdInFrequentPosMap);
        }
        else if (pos == TokenInfo.PosType.DefaultPos)
        {
            Assert.Equal(lid, dec.Token.Lid);
            Assert.Equal(rid, dec.Token.Rid);
        }
        if (value == TokenInfo.ValueType.DefaultValue)
        {
            Assert.Equal(valueId, dec.IdInValueTrie);
        }
    }

    [Fact]
    public void RoundTrip_MultipleTokens_SequentialDecode()
    {
        var tokens = new List<TokenInfo>
        {
            MakeToken(0x123, 0x45, 0x1234, 0x010203, attr: Token.Attribute.SpellingCorrection),
            MakeToken(0x102, 0x102, 0x0234, 0, pos: TokenInfo.PosType.SameAsPrevPos,
                      value: TokenInfo.ValueType.SameAsPrevValue),
            MakeToken(0, 0, 0x0700, 0, pos: TokenInfo.PosType.FrequentPos,
                      value: TokenInfo.ValueType.AsIsKatakana,
                      costType: TokenInfo.CostType.CanUseSmallEncoding, frequentPosId: 3),
        };

        byte[] enc = _codec.EncodeTokens(tokens);

        int p = 0;
        var results = new List<(TokenInfo info, bool next)>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var info = new TokenInfo(new Token());
            bool hasNext = _codec.DecodeToken(enc.AsSpan(p), info, out int read);
            results.Add((info, hasNext));
            p += read;
        }
        Assert.Equal(enc.Length, p);

        // 1: spelling/full pos/normal value
        Assert.True(results[0].next);
        Assert.Equal(Token.Attribute.SpellingCorrection, results[0].info.Token.Attributes);
        Assert.Equal(0x123, results[0].info.Token.Lid);
        Assert.Equal(0x45, results[0].info.Token.Rid);
        Assert.Equal(0x1234, results[0].info.Token.Cost);
        Assert.Equal(0x010203, results[0].info.IdInValueTrie);

        // 2: same-as-prev pos / same-as-prev value
        Assert.True(results[1].next);
        Assert.Equal(TokenInfo.PosType.SameAsPrevPos, results[1].info.Pos);
        Assert.Equal(TokenInfo.ValueType.SameAsPrevValue, results[1].info.Value);
        Assert.Equal(0x0234, results[1].info.Token.Cost);

        // 3: frequent pos / as-is katakana / last
        Assert.False(results[2].next);
        Assert.Equal(TokenInfo.PosType.FrequentPos, results[2].info.Pos);
        Assert.Equal(3, results[2].info.IdInFrequentPosMap);
        Assert.Equal(TokenInfo.ValueType.AsIsKatakana, results[2].info.Value);
        Assert.Equal(0x0700, results[2].info.Token.Cost);
    }

    [Fact]
    public void FirstToken_SameAsPrevPos_Throws()
    {
        var tokens = new List<TokenInfo>
        {
            MakeToken(0, 0, 0x100, 0, pos: TokenInfo.PosType.SameAsPrevPos),
        };
        Assert.Throws<InvalidOperationException>(() => _codec.EncodeTokens(tokens));
    }
}
