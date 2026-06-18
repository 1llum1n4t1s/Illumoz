using System.Collections.Generic;
using System.Text;

namespace Mozc.Rewriter;

// C++ src/rewriter/number_compound_util.cc の中核(数+助数詞の分割)。
// 文字列先頭の数(半角/全角アラビア・漢数字・大字)を取り出し、残りを助数詞とする。
public static class NumberCompoundUtil
{
    [global::System.Flags]
    public enum ScriptType
    {
        None = 0,
        HalfWidthArabic = 1 << 0,
        FullWidthArabic = 1 << 1,
        Kanji = 1 << 2,
        OldKanji = 1 << 3,
    }

    // 漢数字「〇零一二三四五六七八九十百千」のコードポイント集合。
    private static readonly HashSet<int> KanjiDigits = new()
    {
        0x3007, 0x4E00, 0x4E03, 0x4E09, 0x4E5D, 0x4E8C, 0x4E94, 0x516B,
        0x516D, 0x5341, 0x5343, 0x56DB, 0x767E, 0x96F6,
    };

    // 大字「壱弐参」。
    private static readonly HashSet<int> OldKanjiDigits = new() { 0x58F1, 0x5F10, 0x53C2 };

    // input を (数部, 助数詞部) に分割し、数部の字種フラグを返す。
    public static (string Number, string CounterSuffix, ScriptType Type) Split(string input)
    {
        ScriptType type = ScriptType.None;
        var number = new StringBuilder();
        int consumed = 0;
        foreach (System.Text.Rune rune in input.EnumerateRunes())
        {
            int c = rune.Value;
            bool isNumberChar = true;
            if (c >= 0x30 && c <= 0x39)
            {
                type |= ScriptType.HalfWidthArabic;
            }
            else if (c >= 0xFF10 && c <= 0xFF19)
            {
                type |= ScriptType.FullWidthArabic;
            }
            else if (KanjiDigits.Contains(c))
            {
                type |= ScriptType.Kanji;
            }
            else if (OldKanjiDigits.Contains(c))
            {
                type |= ScriptType.OldKanji;
            }
            else
            {
                isNumberChar = false;
            }

            if (!isNumberChar)
            {
                break;
            }
            number.Append(rune.ToString());
            consumed += rune.ToString().Length;
        }

        string num = number.ToString();
        string suffix = input.Substring(consumed);
        return (num, suffix, type);
    }
}
