using System.Text;

namespace Mozc.Base;

// C++ src/base/number_util.cc の一部相当。
// KanjiNumberToArabicNumber は src/data/preedit/kanjinumber-arabicnumber.tsv 由来の
// 表で部分文字列変換する(本家は double-array トライ)。全キーが 1 文字なので
// コードポイント単位の置換と等価。表に無い文字はそのまま通す。
public static class NumberUtil
{
    // kanjinumber-arabicnumber.tsv の全エントリ(漢数字/大字/全角数字 → アラビア数字)。
    private static readonly Dictionary<int, string> KanjiToArabic = new()
    {
        ['零'] = "0", ['一'] = "1", ['二'] = "2", ['三'] = "3", ['四'] = "4",
        ['五'] = "5", ['六'] = "6", ['七'] = "7", ['八'] = "8", ['九'] = "9",
        ['十'] = "10", ['百'] = "100", ['千'] = "1000", ['万'] = "10000",
        ['億'] = "100000000", ['兆'] = "1000000000000", ['京'] = "10000000000000000",
        ['0'] = "0", ['1'] = "1", ['2'] = "2", ['3'] = "3", ['4'] = "4",
        ['5'] = "5", ['6'] = "6", ['7'] = "7", ['8'] = "8", ['9'] = "9",
        ['０'] = "0", ['１'] = "1", ['２'] = "2", ['３'] = "3", ['４'] = "4",
        ['５'] = "5", ['６'] = "6", ['７'] = "7", ['８'] = "8", ['９'] = "9",
        ['壱'] = "1", ['弐'] = "2", ['参'] = "3",
        ['拾'] = "10", ['廿'] = "20", ['卅'] = "30", ['卌'] = "40",
    };

    // 漢数字混じり文字列をアラビア数字へ(表に無い文字はそのまま)。
    public static string KanjiNumberToArabicNumber(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (Rune rune in input.EnumerateRunes())
        {
            sb.Append(KanjiToArabic.TryGetValue(rune.Value, out string? mapped)
                ? mapped
                : rune.ToString());
        }
        return sb.ToString();
    }

    // C++ NumberUtil::IsArabicNumber 相当。全文字が半角/全角数字なら true(空は false)。
    public static bool IsArabicNumber(string input)
    {
        if (input.Length == 0)
        {
            return false;
        }
        foreach (Rune rune in input.EnumerateRunes())
        {
            int c = rune.Value;
            bool isDigit = c is >= '0' and <= '9' || c is >= 0xFF10 and <= 0xFF19;
            if (!isDigit)
            {
                return false;
            }
        }
        return true;
    }

    // C++ NumberUtil::IsDecimalInteger 相当。全文字が半角 ASCII 数字なら true(空は false)。
    public static bool IsDecimalInteger(string str)
    {
        if (str.Length == 0)
        {
            return false;
        }
        foreach (char c in str)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }
        return true;
    }
}
