using System.Collections.Generic;
using System.Text;

namespace Mozc.Rewriter;

// C++ src/rewriter/a11y_description_rewriter.cc の中核(かな/英字の読み上げ説明生成)。
// スクリーンリーダ(TalkBack 等)向けに「あい」→「あい。 ヒラガナ あい」のような
// 文字種を明示した説明を作る。語の説明辞書(SerializedDictionary)依存部は対象外。
public static class A11yDescription
{
    private enum CharType
    {
        Initial, Hiragana, HiraganaSmall, Katakana, KatakanaSmall,
        HalfKatakana, HalfKatakanaSmall, ProlongedSoundMark,
        HalfAlphaLower, HalfAlphaUpper, FullAlphaLower, FullAlphaUpper, Others,
    }

    private static readonly HashSet<int> SmallLetters = new()
    {
        'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ', 'ゃ', 'ゅ', 'ょ', 'っ', 'ゎ',
        'ァ', 'ィ', 'ゥ', 'ェ', 'ォ', 'ャ', 'ュ', 'ョ', 'ッ', 'ヮ',
        'ｧ', 'ｨ', 'ｩ', 'ｪ', 'ｫ', 'ｬ', 'ｭ', 'ｮ', 'ｯ',
    };

    private static readonly Dictionary<int, int> HalfSmallKatakanaToLarge = new()
    {
        ['ｧ'] = 'ｱ', ['ｨ'] = 'ｲ', ['ｩ'] = 'ｳ', ['ｪ'] = 'ｴ', ['ｫ'] = 'ｵ',
        ['ｬ'] = 'ﾔ', ['ｭ'] = 'ﾕ', ['ｮ'] = 'ﾖ', ['ｯ'] = 'ﾂ',
    };

    // value の読み上げ説明を返す(value 本体 + 文字種ラベル付き列)。
    public static string Describe(string value)
    {
        var buf = new StringBuilder(value);
        CharType previous = CharType.Initial;
        CharType current = CharType.Initial;
        foreach (System.Text.Rune rune in value.EnumerateRunes())
        {
            int cp = rune.Value;
            previous = current;
            current = Classify(cp);
            if (current == CharType.Others)
            {
                continue; // 語説明辞書は未対応のためスキップ。
            }
            // 長音/小書きはひらがな/カタカナの直後ではその種別に吸収。
            if ((current == CharType.ProlongedSoundMark
                 || current == CharType.HiraganaSmall
                 || current == CharType.KatakanaSmall)
                && (previous == CharType.Hiragana || previous == CharType.Katakana))
            {
                current = previous;
            }
            buf.Append(KanaLabel(cp, current, previous));
        }
        return buf.ToString();
    }

    private static CharType Classify(int cp)
    {
        if (cp >= 'ぁ' && cp <= 'ん' && cp != 'ゐ' && cp != 'ゑ')
        {
            return SmallLetters.Contains(cp) ? CharType.HiraganaSmall : CharType.Hiragana;
        }
        if ((cp >= 'ァ' && cp <= 'ワ') || cp == 'ヲ' || cp == 'ン')
        {
            return SmallLetters.Contains(cp) ? CharType.KatakanaSmall : CharType.Katakana;
        }
        if ((cp >= 'ｧ' && cp <= 'ﾟ') || (cp >= 'ｦ' && cp <= 'ｯ'))
        {
            return SmallLetters.Contains(cp) ? CharType.HalfKatakanaSmall : CharType.HalfKatakana;
        }
        if (cp == 'ー')
        {
            return CharType.ProlongedSoundMark;
        }
        if (cp >= 'a' && cp <= 'z') { return CharType.HalfAlphaLower; }
        if (cp >= 'A' && cp <= 'Z') { return CharType.HalfAlphaUpper; }
        if (cp >= 'ａ' && cp <= 'ｚ') { return CharType.FullAlphaLower; }
        if (cp >= 'Ａ' && cp <= 'Ｚ') { return CharType.FullAlphaUpper; }
        return CharType.Others;
    }

    private static string KanaLabel(int cp, CharType current, CharType previous)
    {
        // 同種連続(小書き等を除く)は文字のみ。例「あい」→「い」だけ。
        if (previous == current
            && current != CharType.HiraganaSmall
            && current != CharType.KatakanaSmall
            && current != CharType.HalfKatakanaSmall)
        {
            return char.ConvertFromUtf32(cp);
        }

        string prefix;
        switch (current)
        {
            case CharType.Hiragana: prefix = "ヒラガナ "; break;
            case CharType.HiraganaSmall: prefix = "ヒラガナコモジ "; cp++; break;
            case CharType.Katakana: prefix = "カタカナ "; break;
            case CharType.KatakanaSmall: prefix = "カタカナコモジ "; cp++; break;
            case CharType.HalfKatakana: prefix = "ハンカクカタカナ "; break;
            case CharType.HalfKatakanaSmall:
                prefix = "ハンカクカタカナコモジ ";
                cp = HalfSmallKatakanaToLarge.TryGetValue(cp, out int large) ? large : cp;
                break;
            case CharType.ProlongedSoundMark: prefix = "チョウオン "; break;
            case CharType.HalfAlphaLower: prefix = "コモジ "; break;
            case CharType.HalfAlphaUpper: prefix = "オオモジ "; break;
            case CharType.FullAlphaLower: prefix = "ゼンカクコモジ "; break;
            case CharType.FullAlphaUpper: prefix = "ゼンカクオオモジ "; break;
            default: prefix = string.Empty; break;
        }
        return "。" + prefix + char.ConvertFromUtf32(cp);
    }
}
