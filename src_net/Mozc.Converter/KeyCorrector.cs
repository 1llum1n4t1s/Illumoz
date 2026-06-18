using System.Collections.Generic;
using System.Text;

namespace Mozc.Converter;

// C++ src/converter/key_corrector.cc の読み補正中核(文字列補正部)。
// ローマ字入力でよくある打ち間違いをかな列の段階で補正する。
// 例「んあ」→「んな」、「にゃ」→「んや」、「mば」→「んば」、「きっって」→「きって」。
// ※ C++ の lattice 位置アライメント(alignment_/rev_alignment_)は変換器統合用で別途。
public static class KeyCorrector
{
    // input のかな列を補正した文字列を返す。補正対象が無ければ入力と同じ。
    public static string CorrectReading(string input)
    {
        int[] cp = ToCodepoints(input);
        var sb = new StringBuilder(input.Length);
        int i = 0;
        while (i < cp.Length)
        {
            int consumed;
            if (RewriteDoubleNN(cp, i, sb, out consumed)
                || RewriteNN(cp, i, sb, out consumed)
                || RewriteYu(cp, i, sb, out consumed)
                || RewriteNI(cp, i, sb, out consumed)
                || RewriteSmallTsu(cp, i, sb, out consumed)
                || RewriteM(cp, i, sb, out consumed))
            {
                i += consumed;
            }
            else
            {
                sb.Append(char.ConvertFromUtf32(cp[i]));
                i++;
            }
        }
        return sb.ToString();
    }

    private static int[] ToCodepoints(string s)
    {
        var list = new List<int>(s.Length);
        foreach (Rune r in s.EnumerateRunes())
        {
            list.Add(r.Value);
        }
        return list.ToArray();
    }

    private static bool IsHiragana(int c) => c >= 0x3041 && c <= 0x309F;

    // "ん[あいうえお]" -> "ん[なにぬねの]"(先頭以外)
    private static bool RewriteNN(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        if (i == 0 || cp[i] != 0x3093 || i + 1 >= cp.Length)
        {
            return false;
        }
        int outCp = cp[i + 1] switch
        {
            0x3042 => 0x306A, // あ→な
            0x3044 => 0x306B, // い→に
            0x3046 => 0x306C, // う→ぬ
            0x3048 => 0x306D, // え→ね
            0x304A => 0x306E, // お→の
            _ => 0,
        };
        if (outCp == 0)
        {
            return false;
        }
        sb.Append('ん').Append(char.ConvertFromUtf32(outCp));
        consumed = 2;
        return true;
    }

    // "([^ん]hira)んん[あいうえお]" -> 先頭を出し "ん[あいうえお]" を残す(次で RewriteNN)
    // "([^ん]hira)んん[^あいうえお]" -> 先頭+"ん" を出す
    private static bool RewriteDoubleNN(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        if (i + 3 >= cp.Length)
        {
            return false;
        }
        if (!(IsHiragana(cp[i]) && cp[i] != 0x3093) || cp[i + 1] != 0x3093 || cp[i + 2] != 0x3093)
        {
            return false;
        }
        int next = cp[i + 3];
        if (next == 0x3093)
        {
            return false; // "んんん" は無視
        }
        if (next is 0x3042 or 0x3044 or 0x3046 or 0x3048 or 0x304A)
        {
            sb.Append(char.ConvertFromUtf32(cp[i]));
            consumed = 2; // 先頭 + 1つ目の "ん"。2つ目の "ん" 以降は残す。
            return true;
        }
        sb.Append(char.ConvertFromUtf32(cp[i])).Append('ん');
        consumed = 3; // 先頭 + "んん"
        return true;
    }

    // "に[ゃゅょ]" -> "ん[やゆよ]"
    private static bool RewriteNI(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        if (cp[i] != 0x306B || i + 1 >= cp.Length)
        {
            return false;
        }
        int outCp = cp[i + 1] switch
        {
            0x3083 => 0x3084, // ゃ→や
            0x3085 => 0x3086, // ゅ→ゆ
            0x3087 => 0x3088, // ょ→よ
            _ => 0,
        };
        if (outCp == 0)
        {
            return false;
        }
        sb.Append('ん').Append(char.ConvertFromUtf32(outCp));
        consumed = 2;
        return true;
    }

    // "m[ばびぶべぼぱぴぷぺぽ]" -> "ん[...]"(先頭以外、m/ｍ のみ)
    private static bool RewriteM(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        if (i == 0 || (cp[i] != 0x006D && cp[i] != 0xFF4D) || i + 1 >= cp.Length)
        {
            return false;
        }
        int next = cp[i + 1];
        // は..ぽ(0x306F..0x307D)のうち "はひふへほ"(%3==0)以外。
        if (next % 3 != 0 && next >= 0x306F && next <= 0x307D)
        {
            sb.Append('ん').Append(char.ConvertFromUtf32(next));
            consumed = 2;
            return true;
        }
        return false;
    }

    // "([^っ]hira)っっ([^っ]hira)" -> "$1っ$2"
    private static bool RewriteSmallTsu(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        if (i + 3 >= cp.Length)
        {
            return false;
        }
        if (!(IsHiragana(cp[i]) && cp[i] != 0x3063)
            || cp[i + 1] != 0x3063 || cp[i + 2] != 0x3063
            || !(IsHiragana(cp[i + 3]) && cp[i + 3] != 0x3063))
        {
            return false;
        }
        sb.Append(char.ConvertFromUtf32(cp[i])).Append('っ').Append(char.ConvertFromUtf32(cp[i + 3]));
        consumed = 4;
        return true;
    }

    // "[きしちにひり]ゅ[^う]" -> "$1ゅう"(う を挿入、最後の文字は残す)
    private static bool RewriteYu(int[] cp, int i, StringBuilder sb, out int consumed)
    {
        consumed = 0;
        int first = cp[i];
        if (first is not (0x304D or 0x3057 or 0x3061 or 0x306B or 0x3072 or 0x308A))
        {
            return false;
        }
        if (i + 1 >= cp.Length || cp[i + 1] != 0x3085)
        {
            return false; // ゅ
        }
        if (i + 2 >= cp.Length || cp[i + 2] == 0x3046)
        {
            return false; // 末尾が "う" なら補正不要
        }
        sb.Append(char.ConvertFromUtf32(first)).Append('ゅ').Append('う');
        consumed = 2; // 先頭+ゅ を消費。最後の文字は残す。
        return true;
    }
}
