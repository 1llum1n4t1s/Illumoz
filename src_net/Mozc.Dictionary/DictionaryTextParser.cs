namespace Mozc.Dictionary;

// C6 データ生成: 辞書ソーステキスト(src/data/dictionary_oss/dictionaryNN.txt)を Token へ。
// 形式は TAB 区切り 5 列: key \t lid \t rid \t cost \t value
// (C++ の dictionary_builder が読む形式)。SystemDictionaryBuilder への入力になる。
public static class DictionaryTextParser
{
    // 1 行をパース。空行・列不足は null。
    public static Token? ParseLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }
        string[] cols = line.Split('\t');
        if (cols.Length < 5)
        {
            return null;
        }
        if (!ushort.TryParse(cols[1], out ushort lid) ||
            !ushort.TryParse(cols[2], out ushort rid) ||
            !int.TryParse(cols[3], out int cost))
        {
            throw new FormatException($"invalid dictionary line: {line}");
        }
        // 列0=key(読み), 列4=value(表記)。
        return new Token(cols[0], cols[4], cost, lid, rid);
    }

    // 複数行(ファイル内容)をパース。
    public static List<Token> Parse(IEnumerable<string> lines)
    {
        var tokens = new List<Token>();
        foreach (string line in lines)
        {
            Token? t = ParseLine(line);
            if (t != null)
            {
                tokens.Add(t);
            }
        }
        return tokens;
    }
}
