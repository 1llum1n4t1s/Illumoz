using System.Collections.Generic;
using System.Text;

namespace Mozc.Base;

// C++ src/config/character_form_manager.cc の中核スライス。
// スクリプト種別(数字/英字/カタカナ/記号)ごとに全角/半角の好みを保持し、
// 文字列を好みに沿って変換する。LAST_FORM(履歴記憶)とストレージ連携は将来対応。
// 文字種判定は既存の ScriptClassifier(ScriptType)を流用する。
public enum CharacterForm
{
    NoConversion = 0,
    FullWidth = 1,
    HalfWidth = 2,
}

public sealed class CharacterFormManager
{
    // 正規化代表文字(数字→'0'、英字→'A'、カタカナ→'ア'、記号は半角1文字)ごとの好み。
    private readonly Dictionary<char, CharacterForm> _table = new();

    // C++ Preedit の既定ルール(全部 FULL_WIDTH)を構築した manager。
    public static CharacterFormManager CreatePreeditDefault()
    {
        var m = new CharacterFormManager();
        m.AddRule("ア", CharacterForm.FullWidth);
        m.AddRule("A", CharacterForm.FullWidth);
        m.AddRule("0", CharacterForm.FullWidth);
        m.AddRule("(){}[]", CharacterForm.FullWidth);
        m.AddRule(".,", CharacterForm.FullWidth);
        m.AddRule("。、", CharacterForm.FullWidth);
        m.AddRule("・「」", CharacterForm.FullWidth);
        m.AddRule("\"'", CharacterForm.FullWidth);
        m.AddRule(":;", CharacterForm.FullWidth);
        m.AddRule("#%&@$^_|`\\", CharacterForm.FullWidth);
        m.AddRule("~", CharacterForm.FullWidth);
        m.AddRule("<>=+-/*", CharacterForm.FullWidth);
        m.AddRule("?!", CharacterForm.FullWidth);
        return m;
    }

    // config.character_form_rules 相当(group 文字列 + form)からまとめて構築する。
    // C++ では LAST_FORM は履歴記憶だが、未実装のため呼び出し側で FullWidth 等へ
    // 解決した form を渡す。空 group / 空 rules はそのまま無視される。
    public static CharacterFormManager FromRules(
        global::System.Collections.Generic.IEnumerable<(string Group, CharacterForm Form)> rules)
    {
        var m = new CharacterFormManager();
        foreach ((string group, CharacterForm form) in rules)
        {
            if (!string.IsNullOrEmpty(group))
            {
                m.AddRule(group, form);
            }
        }
        return m;
    }

    // input の各文字を正規化代表へ畳んで form を登録する。
    public void AddRule(string input, CharacterForm form)
    {
        foreach (char c in input)
        {
            char norm = NormalizedCharacter(c.ToString());
            if (norm != '\0')
            {
                _table[norm] = form;
            }
        }
    }

    // 文字列(または1文字)の好みを返す。判定不能は NoConversion。
    public CharacterForm GetCharacterForm(string str)
    {
        char norm = NormalizedCharacter(str);
        if (norm == '\0')
        {
            return CharacterForm.NoConversion;
        }
        return _table.TryGetValue(norm, out CharacterForm f) ? f : CharacterForm.NoConversion;
    }

    // str を好みに沿って全角/半角変換する(C++ TryConvertStringWithPreference 相当)。
    // 連続する同 form のランをまとめて変換する。
    public string ConvertString(string str)
    {
        var output = new StringBuilder();
        var buf = new StringBuilder();
        CharacterForm prevForm = CharacterForm.NoConversion;
        ScriptType prevType = ScriptType.Unknown;
        bool first = true;

        foreach (Rune rune in str.EnumerateRunes())
        {
            string ch = rune.ToString();
            ScriptType type = ScriptClassifier.Classify(rune.Value);
            CharacterForm form = prevForm;

            // 記号(Other)は C++ の UNKNOWN_SCRIPT 相当: 毎回 GetCharacterForm。
            // Katakana/Numeric/Alphabet は種別が変わった所で再計算。
            if (type == ScriptType.Other
                || (type == ScriptType.Katakana && prevType != ScriptType.Katakana)
                || (type == ScriptType.Numeric && prevType != ScriptType.Numeric)
                || (type == ScriptType.Alphabet && prevType != ScriptType.Alphabet))
            {
                form = GetCharacterForm(ch);
            }
            else if (type == ScriptType.Kanji || type == ScriptType.Hiragana)
            {
                form = CharacterForm.NoConversion;
            }

            if (!first && prevForm != form)
            {
                output.Append(ConvertWidth(buf.ToString(), prevForm));
                buf.Clear();
            }

            buf.Append(ch);
            prevType = type;
            prevForm = form;
            first = false;
        }

        if (buf.Length > 0)
        {
            output.Append(ConvertWidth(buf.ToString(), prevForm));
        }
        return output.ToString();
    }

    // C++ CharacterFormManager::ConvertWidth 相当。
    public static string ConvertWidth(string input, CharacterForm form) => form switch
    {
        CharacterForm.FullWidth => JapaneseUtil.HalfWidthToFullWidth(input),
        CharacterForm.HalfWidth => JapaneseUtil.FullWidthToHalfWidth(input),
        _ => input,
    };

    // C++ GetNormalizedCharacter 相当。カタカナ→'ア'、数字→'0'、英字→'A'、
    // 記号は半角化した1文字、それ以外は '\0'(変換なし)。
    private static char NormalizedCharacter(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return '\0';
        }
        Rune first = default;
        int count = 0;
        foreach (Rune r in str.EnumerateRunes())
        {
            if (count == 0)
            {
                first = r;
            }
            count++;
        }
        switch (ScriptClassifier.Classify(first.Value))
        {
            case ScriptType.Katakana: return 'ア';
            case ScriptType.Numeric: return '0';
            case ScriptType.Alphabet: return 'A';
            case ScriptType.Kanji:
            case ScriptType.Hiragana: return '\0';
            default: // 記号(Other): 1文字のみ正規化
                if (count != 1)
                {
                    return '\0';
                }
                string half = JapaneseUtil.FullWidthToHalfWidth(str);
                return half.Length >= 1 && half[0] <= 0xFFFF ? half[0] : '\0';
        }
    }
}
