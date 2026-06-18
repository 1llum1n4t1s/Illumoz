using System.Collections.Generic;
using System.Globalization;
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
        ['零'] = "0", ['〇'] = "0", ['一'] = "1", ['二'] = "2", ['三'] = "3", ['四'] = "4",
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

    // 漢数字混じり文字列を「桁を解釈して」アラビア数字へ正規化する
    // (C++ NumberUtil::NormalizeNumbers 相当)。例: "百二十" → "120"、"二百十一" → "211"、
    // "百二十万" → "1200000"。数字以外を含む/解釈できない場合は false。
    // trimLeadingZeros=false なら先頭ゼロの個数を保持する。
    public static bool TryNormalizeNumber(string input, bool trimLeadingZeros, out string arabic)
        => TryNormalizeNumberInternal(input, trimLeadingZeros, allowSuffix: false, out arabic, out _);

    // C++ NumberUtil::NormalizeNumbersWithSuffix 相当。末尾の非数字を suffix として切り出し、
    // 先頭の数字部分のみをアラビア数字へ正規化する(例 "三個" → arabic="3", suffix="個")。
    // 先頭が数字でない/suffix に数字を含む場合は false。
    public static bool TryNormalizeNumberWithSuffix(
        string input, bool trimLeadingZeros, out string arabic, out string suffix)
        => TryNormalizeNumberInternal(input, trimLeadingZeros, allowSuffix: true, out arabic, out suffix);

    private static bool TryNormalizeNumberInternal(
        string input, bool trimLeadingZeros, bool allowSuffix, out string arabic, out string suffix)
    {
        arabic = string.Empty;
        suffix = string.Empty;
        var numbers = new List<ulong>();
        int byteOrCharPos = 0;
        bool suffixFound = false;
        foreach (Rune rune in input.EnumerateRunes())
        {
            string mapped = KanjiNumberToArabicNumber(rune.ToString());
            if (!ulong.TryParse(mapped, out ulong n))
            {
                if (!allowSuffix)
                {
                    return false; // 数字でない文字。
                }
                suffix = input.Substring(byteOrCharPos);
                // "2,000" を "2" + ",000" としないため、suffix に数字があれば失敗。
                if (ContainsDigit(suffix))
                {
                    return false;
                }
                suffixFound = true;
                break;
            }
            numbers.Add(n);
            byteOrCharPos += rune.Utf16SequenceLength;
        }
        _ = suffixFound;
        if (numbers.Count == 0)
        {
            return false;
        }
        if (!NormalizeNumbersHelper(numbers, out ulong value))
        {
            return false;
        }

        var sb = new StringBuilder();
        if (!trimLeadingZeros)
        {
            int numZeros = 0;
            while (numZeros < numbers.Count && numbers[numZeros] == 0)
            {
                numZeros++;
            }
            if (numZeros == numbers.Count)
            {
                numZeros--; // 全部ゼロなら (k-1) 個。
            }
            sb.Append('0', numZeros);
        }
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
        arabic = sb.ToString();
        return true;
    }

    private static bool ContainsDigit(string s)
        => ScriptClassifier.ContainsScriptType(s, ScriptType.Numeric);

    private static bool NormalizeNumbersHelper(List<ulong> numbers, out ulong output)
    {
        output = 0;
        ulong max = 0;
        foreach (ulong v in numbers)
        {
            if (v > max)
            {
                max = v;
            }
        }
        // スケーリング数(10以上)が無ければ単純に 10進連結。
        if (max < 10)
        {
            int idx0 = 0;
            return ReduceLeadingBase10(numbers, ref idx0, out output) && idx0 == numbers.Count;
        }
        return InterpretJapanese(numbers, out output);
    }

    private static bool InterpretJapanese(List<ulong> numbers, out ulong output)
    {
        output = 0;
        ulong lastBase = ulong.MaxValue;
        int i = 0;
        do
        {
            if (!ReduceLessThan10000(numbers, ref i, out ulong coef))
            {
                return false;
            }
            if (i == numbers.Count)
            {
                return TryAdd(output, coef, out output);
            }
            if (numbers[i] >= lastBase)
            {
                return false; // base は降順でなければならない。
            }
            if (!TryMul(coef, numbers[i], out ulong delta) || !TryAdd(output, delta, out output))
            {
                return false;
            }
            lastBase = numbers[i];
            i++;
        }
        while (i < numbers.Count);
        return true;
    }

    private static bool ReduceLeadingBase10(List<ulong> numbers, ref int i, out ulong output)
    {
        output = 0;
        for (; i < numbers.Count; i++)
        {
            if (numbers[i] >= 10)
            {
                break;
            }
            if (!TryMul(output, 10, out output) || !TryAdd(output, numbers[i], out output))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ReduceLessThan10000(List<ulong> numbers, ref int i, out ulong num)
    {
        num = 0;
        bool success = false;
        foreach (ulong expected in new ulong[] { 1000, 100, 10, 1 })
        {
            if (expected == 1)
            {
                if (ReduceOnes(numbers, ref i, out ulong n))
                {
                    num += n;
                    success = true;
                }
            }
            else if (ReduceDigits(numbers, ref i, expected, out ulong n))
            {
                num += n;
                success = true;
            }
        }
        return success && (i == numbers.Count || numbers[i] >= 10000);
    }

    private static bool ReduceOnes(List<ulong> numbers, ref int i, out ulong num)
    {
        num = 0;
        if (i == numbers.Count || numbers[i] >= 10)
        {
            return false;
        }
        num = numbers[i];
        i++;
        return true;
    }

    private static bool ReduceDigits(List<ulong> numbers, ref int i, ulong expectedBase, out ulong num)
    {
        num = 0;
        while (i < numbers.Count && numbers[i] == 0)
        {
            i++; // 先頭ゼロをスキップ。
        }
        if (i == numbers.Count)
        {
            return false;
        }
        ulong leading = numbers[i];
        if (leading < 10)
        {
            if (numbers.Count - i < 2)
            {
                return false;
            }
            ulong next = numbers[i + 1];
            if (next < 10)
            {
                // [1,2,...] => 12 (< expectedBase*10)。
                if (!ReduceLeadingBase10(numbers, ref i, out num)
                    || num >= expectedBase * 10
                    || (i != numbers.Count && numbers[i] < 10000))
                {
                    i = numbers.Count; // 残りを無視。
                    return false;
                }
                return true;
            }
            // [2,10,...] / [1,1000,...]。
            if (next != expectedBase || (leading == 1 && expectedBase != 1000))
            {
                return false;
            }
            num = leading * expectedBase;
            i += 2;
            return true;
        }
        // [10,...] [100,...] [1000,...] [20,...](廿)。
        if (leading == expectedBase || (expectedBase == 10 && leading == 20))
        {
            num = leading;
            i++;
            return true;
        }
        return false;
    }

    private static bool TryAdd(ulong a, ulong b, out ulong r)
    {
        r = unchecked(a + b);
        return r >= a; // オーバーフロー検出。
    }

    private static bool TryMul(ulong a, ulong b, out ulong r)
    {
        r = unchecked(a * b);
        return a == 0 || r / a == b;
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
