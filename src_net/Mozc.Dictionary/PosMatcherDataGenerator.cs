using System.Text.RegularExpressions;

namespace Mozc.Dictionary;

// C6 データ生成: gen_pos_matcher_code.py + pos_util.py 相当。
// id.def(id→POS素性) と special_pos.def, pos_matcher_rule.def(規則名→正規表現)から
// PosMatcher が読む uint16 data を生成する。
// data レイアウト: [N規則のGetId][N規則のoffset][各規則の(lo,hi)…+0xFFFF]。
public static class PosMatcherDataGenerator
{
    // (POS素性文字列, id) を id.def の順 + special_pos.def(max+1から付番)で。
    public static List<(string Feature, int Id)> ParsePosDatabase(
        IEnumerable<string> idDefLines, IEnumerable<string>? specialPosLines = null)
    {
        var list = new List<(string, int)>();
        int maxId = -1;
        foreach (string raw in idDefLines)
        {
            string line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }
            // 「id 素性」(最初の空白で2分割。素性に空白は無い)。
            string[] parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int id))
            {
                continue;
            }
            list.Add((parts[1], id));
            if (id > maxId)
            {
                maxId = id;
            }
        }
        if (specialPosLines != null)
        {
            int next = maxId + 1;
            foreach (string raw in specialPosLines)
            {
                string line = StripComment(raw);
                if (line.Trim().Length == 0)
                {
                    continue; // コメント / 空行 / 空白のみ行はスキップ。
                }
                list.Add((line.Trim(), next));
                next++;
            }
        }
        return list;
    }

    // (規則名, 正規表現) を pos_matcher_rule.def の順で。'*'→'[^,]+'。
    public static List<(string Name, Regex Regex)> ParseRules(IEnumerable<string> ruleLines)
    {
        var rules = new List<(string, Regex)>();
        foreach (string raw in ruleLines)
        {
            string line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }
            string[] parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }
            string pattern = parts[1].Replace("*", "[^,]+");
            rules.Add((parts[0], new Regex(pattern)));
        }
        return rules;
    }

    // 規則にマッチする id を昇順・連続グループ化して (lo,hi) レンジ列に。
    public static List<(int Lo, int Hi)> GetRange(Regex regex, List<(string Feature, int Id)> db)
    {
        var ids = new List<int>();
        foreach ((string feature, int id) in db)
        {
            Match m = regex.Match(feature);
            if (m.Success && m.Index == 0) // Python re.match 相当(先頭一致)
            {
                ids.Add(id);
            }
        }
        ids.Sort();

        var ranges = new List<(int, int)>();
        int start = -1;
        int prev = -1;
        foreach (int id in ids)
        {
            if (start == -1)
            {
                start = prev = id;
            }
            else if (prev + 1 == id)
            {
                prev = id;
            }
            else
            {
                ranges.Add((start, prev));
                start = prev = id;
            }
        }
        if (start != -1)
        {
            ranges.Add((start, prev));
        }
        return ranges;
    }

    // PosMatcher が読む uint16 data を生成。
    public static ushort[] Generate(List<(string Feature, int Id)> db, List<(string Name, Regex Regex)> rules)
    {
        int n = rules.Count;
        var ranges = new List<List<(int Lo, int Hi)>>(n);
        foreach ((_, Regex regex) in rules)
        {
            ranges.Add(GetRange(regex, db));
        }

        var data = new List<ushort>();
        // GetId: 各規則の最小マッチ id(レンジ先頭の lo)。
        foreach (List<(int Lo, int Hi)> r in ranges)
        {
            data.Add((ushort)(r.Count > 0 ? r[0].Lo : 0));
        }
        // offset 表。
        int offset = 2 * n;
        foreach (List<(int Lo, int Hi)> r in ranges)
        {
            data.Add((ushort)offset);
            offset += 2 * r.Count + 1;
        }
        // レンジ本体 + 0xFFFF 番兵。
        foreach (List<(int Lo, int Hi)> r in ranges)
        {
            foreach ((int lo, int hi) in r)
            {
                data.Add((ushort)lo);
                data.Add((ushort)hi);
            }
            data.Add(0xFFFF);
        }
        return data.ToArray();
    }

    private static string StripComment(string line)
    {
        // 行頭 '#' のコメント行はスキップ(行途中は素性に '#' が無い前提)。
        string t = line.TrimEnd('\r', '\n');
        return t.TrimStart().StartsWith('#') ? string.Empty : t;
    }
}
