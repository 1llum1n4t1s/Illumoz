namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/words_info.h の TokenInfo 相当。
// system 辞書のトークン codec が読み書きする中間表現。
public sealed class TokenInfo
{
    public enum PosType
    {
        DefaultPos = 0,
        FrequentPos = 1,
        SameAsPrevPos = 2,
    }

    public enum ValueType
    {
        DefaultValue = 0,
        SameAsPrevValue = 1,
        AsIsHiragana = 2,
        AsIsKatakana = 3,
    }

    public enum CostType
    {
        DefaultCost = 0,
        CanUseSmallEncoding = 1,
    }

    public enum AccentEncodingType
    {
        EncodedInValue = 0,
        EmbeddedInToken = 1,
    }

    public Token Token { get; set; }
    public int IdInValueTrie { get; set; } = -1;
    public int IdInFrequentPosMap { get; set; } = -1;
    public PosType Pos { get; set; } = PosType.DefaultPos;
    public ValueType Value { get; set; } = ValueType.DefaultValue;
    public CostType Cost { get; set; } = CostType.DefaultCost;
    public AccentEncodingType AccentEncoding { get; set; } = AccentEncodingType.EncodedInValue;
    public int AccentType { get; set; } = -1;

    public TokenInfo(Token token) => Token = token;
}
