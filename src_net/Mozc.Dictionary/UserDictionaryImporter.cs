using System.Collections.Generic;

namespace Mozc.Dictionary;

// C++ src/dictionary/user_dictionary_importer.cc 相当(フォーマット判定 + 行解析部)。
// 他 IME(MS-IME/ATOK/ことえり/Gboard)や Mozc 形式のテキストを読み込む。
public static class UserDictionaryImporter
{
    public enum ImeType
    {
        AutoDetect,
        Mozc,
        Msime,
        Atok,
        Kotoeri,
        GboardV1,
        None, // NUM_IMES 相当(判定不能)
    }

    public readonly record struct RawEntry(string Reading, string Word, string Pos, string Comment);

    // 先頭行などからフォーマットを推測する(C++ GuessIMEType)。
    public static ImeType GuessImeType(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return ImeType.None;
        }
        string lower = line.ToLowerInvariant();

        if (lower.StartsWith("!microsoft ime"))
        {
            return ImeType.Msime;
        }
        if (lower.StartsWith("!!dicut") && lower.Length > 7)
        {
            string version = lower.Substring(7);
            return TryLeadingInt(version, out int v) && v >= 11 ? ImeType.Atok : ImeType.None;
        }
        if (lower.StartsWith("!!atok_tango_text_header"))
        {
            return ImeType.Atok;
        }
        if (line[0] == '"' && line[^1] == '"' && !line.Contains('\t'))
        {
            return ImeType.Kotoeri;
        }
        if (lower.StartsWith("# gboard dictionary version:1"))
        {
            return ImeType.GboardV1;
        }
        if (line[0] == '#' || line.Contains('\t'))
        {
            return ImeType.Mozc;
        }
        return ImeType.None;
    }

    // ユーザー指定型と推測型から最終的に使う型を決める(C++ DetermineFinalIMEType)。
    public static ImeType DetermineFinalImeType(ImeType userType, ImeType guessedType)
    {
        if (userType == ImeType.AutoDetect)
        {
            return guessedType;
        }
        if (userType == ImeType.Mozc)
        {
            // Mozc は MS-IME/ATOK 互換。判定失敗でも Mozc 形式を試す。
            return guessedType != ImeType.Kotoeri ? userType : ImeType.None;
        }
        // ATOK/MS-IME/ことえりは 100% 判定可能。
        return guessedType == userType ? userType : ImeType.None;
    }

    // 1行を RawEntry へ。コメント行・フィールド不足は null。
    public static RawEntry? ParseLine(ImeType type, string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }
        // コメント行スキップ。
        if (((type == ImeType.Msime || type == ImeType.Atok) && line[0] == '!')
            || ((type == ImeType.Mozc || type == ImeType.GboardV1) && line[0] == '#')
            || (type == ImeType.Kotoeri && line.StartsWith("//")))
        {
            return null;
        }

        List<string> values = type == ImeType.Kotoeri
            ? SplitCsv(line)
            : new List<string>(line.Split('\t'));

        if (values.Count < 3)
        {
            return null;
        }
        string reading = values[0];
        string word = values[1];
        string pos = type == ImeType.GboardV1 ? "品詞なし:" + values[2] : values[2];
        string comment = values.Count >= 4 ? values[3] : string.Empty;
        return new RawEntry(reading, word, pos, comment);
    }

    // 先頭の連続数字を整数化(version 判定用)。
    private static bool TryLeadingInt(string s, out int value)
    {
        int i = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            i++;
        }
        if (i == 0)
        {
            value = 0;
            return false;
        }
        return int.TryParse(s.AsSpan(0, i), out value);
    }

    // ことえり用の最小 CSV 分割(ダブルクオート対応、"" はエスケープ)。
    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new global::System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
