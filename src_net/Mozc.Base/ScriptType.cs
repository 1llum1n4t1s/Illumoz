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

    // 文字列全体の文字種(C++ Util::GetScriptType(string_view) 相当)。
    // 全コードポイントが同一文字種ならその種別、混在なら Unknown。
    // 長音「ー」・中点「・」・濁点/半濁点(U+3099..309C)はかな扱い、
    // 2文字目以降の「.」「．」は数字の一部とみなす。
    public static ScriptType GetScriptType(string str) => GetScriptTypeInternal(str, false);

    // 記号(Other)を無視して判定(C++ GetScriptTypeWithoutSymbols 相当)。
    public static ScriptType GetScriptTypeWithoutSymbols(string str) => GetScriptTypeInternal(str, true);

    private static ScriptType GetScriptTypeInternal(string str, bool ignoreSymbols)
    {
        var all = new[] { ScriptType.Unknown, ScriptType.Hiragana, ScriptType.Katakana,
            ScriptType.Kanji, ScriptType.Numeric, ScriptType.Alphabet, ScriptType.Other };
        var bs = new HashSet<ScriptType>(all);
        var kana = new HashSet<ScriptType> { ScriptType.Hiragana, ScriptType.Katakana };

        foreach (Rune rune in str.EnumerateRunes())
        {
            if (bs.Count == 0)
            {
                return ScriptType.Unknown;
            }
            int c = rune.Value;
            // 長音/中点/濁点・半濁点はひらがな・カタカナ両方に属する。
            if (c == 0x30FC || c == 0x30FB || (c >= 0x3099 && c <= 0x309C))
            {
                bs.IntersectWith(kana);
                continue;
            }
            // 先頭以外の「.」「．」は数字の一部。
            if ((c == 0xFF0E || c == '.') && bs.Count == 1 && bs.Contains(ScriptType.Numeric))
            {
                continue;
            }
            ScriptType type = Classify(c);
            // 記号(C++ では UNKNOWN_SCRIPT)を無視する場合。
            if (ignoreSymbols && type == ScriptType.Other)
            {
                continue;
            }
            bs.IntersectWith(new[] { type });
        }

        return bs.Count == 1 ? bs.Single() : ScriptType.Unknown;
    }

    // C++ Util::IsEnglishTransliteration 相当。空白/!/'/- と A-Z/a-z のみなら true。
    public static bool IsEnglishTransliteration(string value)
    {
        foreach (char ch in value)
        {
            bool ok = ch is ' ' or '!' or '\'' or '-'
                or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
            if (!ok)
            {
                return false;
            }
        }
        return true;
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
