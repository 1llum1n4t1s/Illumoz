using System.Text;

namespace Mozc.Base;

// C++ src/base/util.cc の ScriptType(必要部分)。文字種判定。
public enum ScriptType
{
    Unknown,
    Hiragana,
    Katakana,
    Kanji,
    Numeric,
    Alphabet,
    Other,
}

public static class ScriptClassifier
{
    // 先頭のコードポイントの文字種を返す(IsValidKey 等の判定用)。
    public static ScriptType GetFirstScriptType(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            return Classify(rune.Value);
        }
        return ScriptType.Unknown;
    }

    public static ScriptType Classify(int c)
    {
        // ひらがな
        if (c is >= 0x3041 and <= 0x309F)
        {
            return ScriptType.Hiragana;
        }
        // カタカナ(全角 + 拡張 + 半角)
        if (c is >= 0x30A1 and <= 0x30FF
            || c is >= 0x31F0 and <= 0x31FF
            || c is >= 0xFF66 and <= 0xFF9D)
        {
            return ScriptType.Katakana;
        }
        // 漢字(CJK 統合 + 拡張A + 互換 + SIP)
        if (c is >= 0x3400 and <= 0x4DBF
            || c is >= 0x4E00 and <= 0x9FFF
            || c is >= 0xF900 and <= 0xFAFF
            || c is >= 0x20000 and <= 0x2FFFF)
        {
            return ScriptType.Kanji;
        }
        if (c is >= '0' and <= '9' || c is >= 0xFF10 and <= 0xFF19)
        {
            return ScriptType.Numeric;
        }
        if (c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z'
            || c is >= 0xFF21 and <= 0xFF3A || c is >= 0xFF41 and <= 0xFF5A)
        {
            return ScriptType.Alphabet;
        }
        return ScriptType.Other;
    }
}
