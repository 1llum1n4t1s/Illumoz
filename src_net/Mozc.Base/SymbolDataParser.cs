using System.Globalization;

namespace Mozc.Base;

// src/data の symbol.tsv / single_kanji.tsv / emoji_data.tsv を (読み -> 値列) へ
// パースする中立ロジック(Rewriter からも DataGen からも共有する単一の真実)。
public static class SymbolDataParser
{
    // symbol.tsv: ヘッダ1行 + 列1=記号, 列2=スペース区切り読み。ひらがな読みのみ採用、出現順保持。
    public static IReadOnlyDictionary<string, string[]> ParseSymbol(string tsv)
        => ParseGlyphReadings(tsv, skipFirstLine: true, commentHash: false);

    // emoji_data.tsv: # コメント, 列1=絵文字, 列2=スペース区切り読み。
    public static IReadOnlyDictionary<string, string[]> ParseEmoji(string tsv)
        => ParseGlyphReadings(tsv, skipFirstLine: false, commentHash: true);

    // single_kanji.tsv: 列0=読み, 列1=単漢字連結。書記素単位で 1 候補。
    public static IReadOnlyDictionary<string, string[]> ParseSingleKanji(string tsv)
    {
        var dict = new Dictionary<string, string[]>();
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (row.Length == 0 || row[0] == '#')
            {
                continue;
            }
            int tab = row.IndexOf('\t');
            if (tab <= 0 || tab == row.Length - 1)
            {
                continue;
            }
            string reading = row.Substring(0, tab);
            string chars = row.Substring(tab + 1);
            var list = new List<string>();
            TextElementEnumerator en = StringInfo.GetTextElementEnumerator(chars);
            while (en.MoveNext())
            {
                list.Add((string)en.Current);
            }
            if (list.Count > 0)
            {
                dict[reading] = list.ToArray();
            }
        }
        return dict;
    }

    private static IReadOnlyDictionary<string, string[]> ParseGlyphReadings(
        string tsv, bool skipFirstLine, bool commentHash)
    {
        var acc = new Dictionary<string, List<string>>();
        bool first = true;
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (skipFirstLine && first)
            {
                first = false;
                continue;
            }
            first = false;
            if (row.Length == 0 || (commentHash && row[0] == '#'))
            {
                continue;
            }
            string[] cols = row.Split('\t');
            if (cols.Length < 3 || cols[1].Length == 0)
            {
                continue;
            }
            string glyph = cols[1];
            foreach (string reading in cols[2].Split(' ', global::System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IsHiragana(reading))
                {
                    continue;
                }
                if (!acc.TryGetValue(reading, out List<string>? list))
                {
                    acc[reading] = list = new List<string>();
                }
                if (!list.Contains(glyph))
                {
                    list.Add(glyph);
                }
            }
        }
        var dict = new Dictionary<string, string[]>(acc.Count);
        foreach (KeyValuePair<string, List<string>> kv in acc)
        {
            dict[kv.Key] = kv.Value.ToArray();
        }
        return dict;
    }

    public static bool IsHiragana(string s)
    {
        foreach (char c in s)
        {
            if (c is < 'ぁ' or > 'ゖ' && c != 'ー' && c != '゛' && c != '゜')
            {
                return false;
            }
        }
        return s.Length > 0;
    }
}
