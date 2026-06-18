using System.Text;

namespace Mozc.Base;

// C++ src/base/strings/japanese.cc の一部相当(かな変換)。
// 本家は double-array トライ表(japanese_rules.cc)で部分文字列変換するが、
// ひらがな⇔カタカナの標準域はコードポイント +0x60 の 1:1 写像で表と一致する
// (ぁ U+3041→ァ U+30A1 … ゔ U+3094→ヴ U+30F4)。
// system 辞書の builder/reader が同一関数を使えば往復は厳密。
// NOTE: 反復記号(ゝゞ)・半角カナ・濁点正規化など本家の表全域は未対応。
// 実 mozc.data 読取時のバイト厳密一致が要るならば生成表の移植が必要。
public static class JapaneseUtil
{
    private const int HiraganaStart = 0x3041; // ぁ
    private const int HiraganaEnd = 0x3094;   // ゔ
    private const int KatakanaStart = 0x30A1; // ァ
    private const int KatakanaEnd = 0x30F4;   // ヴ
    private const int Offset = KatakanaStart - HiraganaStart; // 0x60

    public static string HiraganaToKatakana(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (Rune rune in input.EnumerateRunes())
        {
            int c = rune.Value;
            if (c >= HiraganaStart && c <= HiraganaEnd)
            {
                sb.Append((char)(c + Offset));
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }
        return sb.ToString();
    }

    public static string KatakanaToHiragana(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (Rune rune in input.EnumerateRunes())
        {
            int c = rune.Value;
            if (c >= KatakanaStart && c <= KatakanaEnd)
            {
                sb.Append((char)(c - Offset));
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }
        return sb.ToString();
    }
}
