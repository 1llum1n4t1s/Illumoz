using System.Collections.Generic;
using System.Text;

namespace Mozc.Base;

// C++ src/base/util.cc の Util::SplitStringToUtf8Graphemes 相当。
// 文字列を書記素クラスタ(grapheme cluster)へ分割する。結合濁点・SVS/IVS・
// 各種絵文字シーケンス(presentation/modifier/flag/tag/keycap/ZWJ)を前の要素に結合する。
public static class GraphemeSplitter
{
    public static List<string> Split(string str)
    {
        // まずコードポイント単位へ。
        var codepoints = new List<int>();
        var chars = new List<string>();
        foreach (System.Text.Rune r in str.EnumerateRunes())
        {
            codepoints.Add(r.Value);
            chars.Add(r.ToString());
        }
        if (chars.Count <= 1)
        {
            return chars;
        }

        const int standalone = 0x0000;
        int prev = standalone;
        var result = new List<string>(chars.Count);

        for (int i = 0; i < chars.Count; i++)
        {
            int cp = codepoints[i];
            string g = chars[i];

            bool isDakuten = cp == 0x3099 || cp == 0x309A;
            bool isSvs = cp >= 0xFE00 && cp <= 0xFE0F;
            bool isIvs = cp >= 0xE0100 && cp <= 0xE01EF;
            bool isTextPresentation = cp == 0xFE0E;     // VS15
            bool isEmojiPresentation = cp == 0xFE0F;    // VS16
            bool isEmojiModifier = cp >= 0x1F3FB && cp <= 0x1F3FF;
            bool isEmojiFlag = prev >= 0x1F1E6 && prev <= 0x1F1FF
                && cp >= 0x1F1E6 && cp <= 0x1F1FF;
            bool isEmojiTag = (cp >= 0xE0020 && cp <= 0xE007E) || cp == 0xE007F;
            // キーキャップ(U+20E3)は VS16 経由だけでなく、省略形 "1⃣" のように
            // 基底文字([0-9#*])直後も同一クラスタにする。
            bool isEmojiKeycap = cp == 0x20E3 && (prev == 0xFE0F || IsKeycapBase(prev));
            bool isEmojiZwj = cp == 0x200D || prev == 0x200D;

            if (isEmojiFlag && result.Count > 0)
            {
                result[^1] += g;
                prev = standalone; // 3つ目の地域表示子を別クラスタに。
                continue;
            }

            if ((isDakuten || isSvs || isIvs || isTextPresentation || isEmojiPresentation
                 || isEmojiModifier || isEmojiTag || isEmojiKeycap || isEmojiZwj)
                && result.Count > 0)
            {
                result[^1] += g;
            }
            else
            {
                result.Add(g);
            }
            prev = cp;
        }
        return result;
    }

    // キーキャップ列の基底文字(0-9 / # / *)。
    private static bool IsKeycapBase(int cp)
        => (cp >= '0' && cp <= '9') || cp == '#' || cp == '*';
}
