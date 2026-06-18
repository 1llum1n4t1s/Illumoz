using System.Text.RegularExpressions;
using Mozc.Dictionary;

namespace Mozc.Converter;

// C6: gen_segmenter_code.py 相当(規則→is_boundary)。
// segmenter.def の各行 `左POSパターン 右POSパターン bool`。
// is_boundary(rid,lid): rid==0||lid==0 → true(BOS/EOS)。以降、最初にマッチした規則の bool。
// どれにもマッチしなければ default true。パターン '*'→任意一致, それ以外は '*'→'[^,]+' で
// 正規表現化し POS 素性の先頭一致。
public static class SegmenterRuleMatcher
{
    public sealed class Rule
    {
        public Regex? Left;   // null = "*"(任意一致)
        public Regex? Right;
        public bool Value;
    }

    public static List<Rule> ParseRules(IEnumerable<string> lines)
    {
        var rules = new List<Rule>();
        foreach (string raw in lines)
        {
            string line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                continue;
            }
            rules.Add(new Rule
            {
                Left = ToRegex(parts[0]),
                Right = ToRegex(parts[1]),
                Value = parts[2].Equals("true", StringComparison.OrdinalIgnoreCase),
            });
        }
        return rules;
    }

    private static Regex? ToRegex(string pattern)
        => pattern == "*" ? null : new Regex(pattern.Replace("*", "[^,]+"));

    // POS データベースと規則から is_boundary(rid,lid) を構築。
    // 各規則の左/右パターンの id 一致を事前計算して O(規則数) 判定にする。
    public static Func<int, int, bool> BuildIsBoundary(
        List<(string Feature, int Id)> db, List<Rule> rules)
    {
        int maxId = 0;
        foreach ((_, int id) in db)
        {
            if (id > maxId)
            {
                maxId = id;
            }
        }
        int size = maxId + 1;

        var leftMatch = new bool[rules.Count][];
        var rightMatch = new bool[rules.Count][];
        for (int i = 0; i < rules.Count; i++)
        {
            leftMatch[i] = new bool[size];
            rightMatch[i] = new bool[size];
            foreach ((string feature, int id) in db)
            {
                leftMatch[i][id] = Matches(rules[i].Left, feature);
                rightMatch[i][id] = Matches(rules[i].Right, feature);
            }
        }

        return (rid, lid) =>
        {
            if (rid == 0 || lid == 0)
            {
                return true;
            }
            for (int i = 0; i < rules.Count; i++)
            {
                if ((uint)rid < (uint)size && (uint)lid < (uint)size &&
                    leftMatch[i][rid] && rightMatch[i][lid])
                {
                    return rules[i].Value;
                }
            }
            return true; // default
        };
    }

    private static bool Matches(Regex? regex, string feature)
    {
        if (regex == null)
        {
            return true; // "*"
        }
        Match m = regex.Match(feature);
        return m.Success && m.Index == 0; // Python re.match
    }

    public static int PosCount(List<(string Feature, int Id)> db)
    {
        int maxId = 0;
        foreach ((_, int id) in db)
        {
            if (id > maxId)
            {
                maxId = id;
            }
        }
        return maxId + 1;
    }
}
