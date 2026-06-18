using System.Collections.Generic;
using System.Text;
using Mozc.Base;

namespace Mozc.Composer;

// C++ composer::Composer::TransformCharactersForNumbers の移植。
// 英数字に隣接する日本語記号(ー/、/。)を数式向けの記号(−/，/．)へ変換する。
// 例: "1ー2" → "1−2"、"0、5" → "0，5"。英数字も記号も含まない場合は変更なし。
public static class NumberCharTransformer
{
    private enum Script { JaHyphen, JaComma, JaPeriod, Alphabet, Number, Other }

    // 変換が起きたら (true, 変換後)、起きなければ (false, 入力そのまま)。
    public static (bool Changed, string Result) Transform(string query)
    {
        var runes = new List<System.Text.Rune>();
        foreach (System.Text.Rune r in query.EnumerateRunes())
        {
            runes.Add(r);
        }
        var scripts = new List<Script>(runes.Count);
        bool hasSymbols = false;
        bool hasAlphanumerics = false;
        foreach (System.Text.Rune r in runes)
        {
            switch (r.Value)
            {
                case 0x30FC: hasSymbols = true; scripts.Add(Script.JaHyphen); break; // ー
                case 0x3001: hasSymbols = true; scripts.Add(Script.JaComma); break;  // 、
                case 0x3002: hasSymbols = true; scripts.Add(Script.JaPeriod); break; // 。
                case '+': case '*': case '/': case '=': case '(': case ')': case '<': case '>':
                case 0xFF0B: case 0xFF0A: case 0xFF0F: case 0xFF1D:
                case 0xFF08: case 0xFF09: case 0xFF1C: case 0xFF1E:
                    scripts.Add(Script.Alphabet);
                    break;
                default:
                    ScriptType t = ScriptClassifier.Classify(r.Value);
                    if (t == ScriptType.Numeric) { hasAlphanumerics = true; scripts.Add(Script.Number); }
                    else if (t == ScriptType.Alphabet) { hasAlphanumerics = true; scripts.Add(Script.Alphabet); }
                    else { scripts.Add(Script.Other); }
                    break;
            }
        }

        if (!hasAlphanumerics || !hasSymbols)
        {
            return (false, query);
        }

        var sb = new StringBuilder();
        bool transformed = false;
        for (int i = 0; i < runes.Count; i++)
        {
            string? append = null;
            switch (scripts[i])
            {
                case Script.JaHyphen:
                {
                    bool check;
                    if (i == 0 && runes.Count > 1)
                    {
                        check = scripts[1] == Script.Number;
                    }
                    else
                    {
                        check = false;
                        for (int j = i; j > 0; j--)
                        {
                            if (scripts[j - 1] == Script.JaHyphen)
                            {
                                continue;
                            }
                            check = IsAlphabetOrNumber(scripts[j - 1]);
                            break;
                        }
                    }
                    if (check)
                    {
                        append = "−"; // − (MINUS SIGN)
                    }
                    break;
                }
                case Script.JaComma:
                    if (i > 0 && IsAlphabetOrNumber(scripts[i - 1]))
                    {
                        append = "，"; // ，
                    }
                    break;
                case Script.JaPeriod:
                    if (i > 0 && IsAlphabetOrNumber(scripts[i - 1]))
                    {
                        append = "．"; // ．
                    }
                    break;
            }

            if (append == null)
            {
                sb.Append(runes[i].ToString());
            }
            else
            {
                sb.Append(append);
                transformed = true;
            }
        }
        return transformed ? (true, sb.ToString()) : (false, query);
    }

    private static bool IsAlphabetOrNumber(Script s) => s == Script.Alphabet || s == Script.Number;
}
