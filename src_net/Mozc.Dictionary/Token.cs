namespace Mozc.Dictionary;

// C++ src/dictionary/dictionary_token.h の Token 相当。辞書1エントリ。
public sealed class Token
{
    [Flags]
    public enum Attribute : byte
    {
        None = 0,
        SpellingCorrection = 1,
        SuffixDictionary = 1 << 6,
        UserDictionary = 1 << 7,
    }

    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Cost { get; set; }
    public ushort Lid { get; set; }
    public ushort Rid { get; set; }
    public Attribute Attributes { get; set; } = Attribute.None;

    public Token() { }

    public Token(string key, string value, int cost, ushort lid, ushort rid,
        Attribute attributes = Attribute.None)
    {
        Key = key;
        Value = value;
        Cost = cost;
        Lid = lid;
        Rid = rid;
        Attributes = attributes;
    }
}
