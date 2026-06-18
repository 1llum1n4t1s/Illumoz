namespace Mozc.Base;

// C++ src/base/strings/japanese.cc 相当。japanese_rules.cc の double-array 表
// (JapaneseRules)を JapaneseTextConverter で引いて部分文字列変換する。
// 反復記号・半角カナ・濁点正規化など本家の表全域を忠実移植済(バイト厳密一致)。
public static class JapaneseUtil
{
    public static string HiraganaToKatakana(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.hiragana_to_katakana_da, JapaneseRules.hiragana_to_katakana_table, input);

    public static string KatakanaToHiragana(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.katakana_to_hiragana_da, JapaneseRules.katakana_to_hiragana_table, input);

    public static string HiraganaToRomanji(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.hiragana_to_romanji_da, JapaneseRules.hiragana_to_romanji_table, input);

    public static string RomanjiToHiragana(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.romanji_to_hiragana_da, JapaneseRules.romanji_to_hiragana_table, input);

    public static string HalfWidthAsciiToFullWidthAscii(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.halfwidthascii_to_fullwidthascii_da,
            JapaneseRules.halfwidthascii_to_fullwidthascii_table, input);

    public static string FullWidthAsciiToHalfWidthAscii(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.fullwidthascii_to_halfwidthascii_da,
            JapaneseRules.fullwidthascii_to_halfwidthascii_table, input);

    public static string HalfWidthKatakanaToFullWidthKatakana(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.halfwidthkatakana_to_fullwidthkatakana_da,
            JapaneseRules.halfwidthkatakana_to_fullwidthkatakana_table, input);

    public static string FullWidthKatakanaToHalfWidthKatakana(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.fullwidthkatakana_to_halfwidthkatakana_da,
            JapaneseRules.fullwidthkatakana_to_halfwidthkatakana_table, input);

    public static string HiraganaToFullwidthRomanji(string input) =>
        HalfWidthAsciiToFullWidthAscii(HiraganaToRomanji(input));

    public static string NormalizeVoicedSoundMark(string input) =>
        JapaneseTextConverter.Convert(
            JapaneseRules.normalize_voiced_sound_da,
            JapaneseRules.normalize_voiced_sound_table, input);

    public static string FullWidthToHalfWidth(string input) =>
        FullWidthKatakanaToHalfWidthKatakana(FullWidthAsciiToHalfWidthAscii(input));

    public static string HalfWidthToFullWidth(string input) =>
        HalfWidthKatakanaToFullWidthKatakana(HalfWidthAsciiToFullWidthAscii(input));
}
