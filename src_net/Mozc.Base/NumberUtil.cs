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

    // 数値表記の 1 候補。Value=表記、Description=種別説明。
    public readonly record struct NumberString(string Value, string Description);

    private static readonly char[] WideDigits =
        { '０', '１', '２', '３', '４', '５', '６', '７', '８', '９' };
    private static readonly string[] KanjiDigits =
        { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] OldKanjiDigits =
        { "〇", "壱", "弐", "参", "四", "伍", "六", "七", "八", "九" };
    private static readonly string[] SmallUnits = { "", "十", "百", "千" };
    private static readonly string[] OldSmallUnits = { "", "拾", "百", "阡" };
    private static readonly string[] BigUnits = { "", "万", "億", "兆", "京" };
    private static readonly string[] OldBigUnits = { "", "萬", "億", "兆", "京" };

    // C++ NumberUtil::ArabicToWideArabic / ArabicToSeparatedArabic / ArabicToKanji 相当(整数のみ)。
    // 半角アラビア整数文字列から各表記候補を生成する。先頭ゼロや 16 桁超は空を返す。
    public static IReadOnlyList<NumberString> ArabicToVariants(string arabic)
    {
        var results = new List<NumberString>();
        if (!IsDecimalInteger(arabic) || arabic.Length > 16)
        {
            return results;
        }
        // 先頭ゼロ(「007」等)はゼロ落とし表記が崩れるため正規化値で扱う。
        string norm = arabic.TrimStart('0');
        if (norm.Length == 0)
        {
            norm = "0";
        }

        results.Add(new NumberString(ToWide(norm), "全角"));
        results.Add(new NumberString(Separate(norm), "区切り"));
        string kanji = ToKanjiPositional(norm, KanjiDigits, SmallUnits, BigUnits);
        if (kanji.Length > 0)
        {
            results.Add(new NumberString(kanji, "漢数字"));
        }
        string old = ToKanjiPositional(norm, OldKanjiDigits, OldSmallUnits, OldBigUnits);
        if (old.Length > 0)
        {
            results.Add(new NumberString(old, "大字"));
        }

        // 小さい数は丸数字・ローマ数字も提示(C++ kArabicNumber 系)。
        if (int.TryParse(norm, out int v))
        {
            if (v >= 1 && v <= 20)
            {
                results.Add(new NumberString(char.ConvertFromUtf32(0x2460 + v - 1), "丸数字")); // ①..⑳
            }
            string? roman = ToRoman(v);
            if (roman != null)
            {
                results.Add(new NumberString(roman, "ローマ数字大")); // ⅠⅡⅢ..
                results.Add(new NumberString(roman.ToLowerInvariant(), "ローマ数字小"));
            }
        }
        return results;
    }

    // 1..12 を Unicode ローマ数字(Ⅰ..Ⅻ)へ。範囲外は null。
    private static string? ToRoman(int v)
    {
        if (v is < 1 or > 12)
        {
            return null;
        }
        return char.ConvertFromUtf32(0x2160 + v - 1); // Ⅰ=U+2160
    }

    private static string ToWide(string arabic)
    {
        var sb = new StringBuilder(arabic.Length);
        foreach (char c in arabic)
        {
            sb.Append(WideDigits[c - '0']);
        }
        return sb.ToString();
    }

    // 3 桁ごとにカンマ区切り。
    private static string Separate(string arabic)
    {
        var sb = new StringBuilder(arabic.Length + arabic.Length / 3);
        int first = arabic.Length % 3;
        if (first == 0)
        {
            first = 3;
        }
        sb.Append(arabic, 0, first);
        for (int i = first; i < arabic.Length; i += 3)
        {
            sb.Append(',').Append(arabic, i, 3);
        }
        return sb.ToString();
    }

    // 位取り漢数字(千二百三十四 / 大字)。4 桁ごとに 万億兆京 を付す。
    private static string ToKanjiPositional(string arabic, string[] digits, string[] small, string[] big)
    {
        if (arabic == "0")
        {
            return digits[0];
        }
        // 4 桁グループに分割(下位から)。
        var groups = new List<string>();
        for (int end = arabic.Length; end > 0; end -= 4)
        {
            int start = global::System.Math.Max(0, end - 4);
            groups.Add(arabic.Substring(start, end - start));
        }
        if (groups.Count > big.Length)
        {
            return string.Empty; // 京超は非対応。
        }
        var sb = new StringBuilder();
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            string part = FourDigitToKanji(groups[g], digits, small);
            if (part.Length == 0)
            {
                continue; // この桁グループが 0。
            }
            sb.Append(part).Append(big[g]);
        }
        return sb.ToString();
    }

    // 1〜4 桁を漢数字へ(「一十」は「十」、「一百」は「百」と省く)。
    private static string FourDigitToKanji(string group, string[] digits, string[] small)
    {
        int val = int.Parse(group);
        if (val == 0)
        {
            return string.Empty;
        }
        var sb = new StringBuilder();
        int len = group.Length;
        for (int i = 0; i < len; i++)
        {
            int d = group[i] - '0';
            int unitPos = len - 1 - i; // 0=一の位,1=十,2=百,3=千
            if (d == 0)
            {
                continue;
            }
            // 十/百/千の位で digit が 1 のときは「一」を省く。
            if (!(d == 1 && unitPos >= 1))
            {
                sb.Append(digits[d]);
            }
            sb.Append(small[unitPos]);
        }
        return sb.ToString();
    }
}
