using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Mozc.Converter;

// C6: gen_boundary_data.py 相当。boundary.def(PREFIX/SUFFIX pattern cost)から
// Segmenter の "bdry" セクション(uint16[2N]: [2*id]=prefix penalty, [2*id+1]=suffix penalty)
// を生成。pattern は '^' + ('*'→'[^,]+') で先頭一致。最初にマッチした規則の cost、無ければ 0。
// special pos は (0,0)。
public static class BoundaryDataGenerator
{
    public static (List<(Regex Regex, int Cost)> Prefix, List<(Regex Regex, int Cost)> Suffix)
        ParsePatterns(IEnumerable<string> boundaryDefLines)
    {
        var prefix = new List<(Regex, int)>();
        var suffix = new List<(Regex, int)>();
        foreach (string raw in boundaryDefLines)
        {
            string line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }
            string[] f = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 3)
            {
                continue;
            }
            var regex = new Regex("^" + f[1].Replace("*", "[^,]+"));
            int cost = int.Parse(f[2]);
            if (f[0] == "PREFIX")
            {
                prefix.Add((regex, cost));
            }
            else if (f[0] == "SUFFIX")
            {
                suffix.Add((regex, cost));
            }
        }
        return (prefix, suffix);
    }

    private static int GetCost(List<(Regex Regex, int Cost)> patterns, string feature)
    {
        foreach ((Regex regex, int cost) in patterns)
        {
            if (regex.IsMatch(feature))
            {
                return cost;
            }
        }
        return 0;
    }

    // features は id 順(index=id)の POS 素性。numSpecialPos は special_pos の件数。
    public static byte[] Generate(
        IReadOnlyList<string> featuresByaId, int numSpecialPos,
        List<(Regex Regex, int Cost)> prefix, List<(Regex Regex, int Cost)> suffix)
    {
        int total = featuresByaId.Count + numSpecialPos;
        var bytes = new byte[total * 4]; // 2 * uint16 per id
        int p = 0;
        foreach (string feature in featuresByaId)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(p), (ushort)GetCost(prefix, feature));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(p + 2), (ushort)GetCost(suffix, feature));
            p += 4;
        }
        // special pos は prefix/suffix とも 0(既に 0 埋め)。
        return bytes;
    }

    // (feature, id) リスト(PosMatcherDataGenerator.ParsePosDatabase 形式)を id 順の features に。
    public static List<string> FeaturesById(List<(string Feature, int Id)> db)
    {
        int maxId = 0;
        foreach ((_, int id) in db)
        {
            if (id > maxId)
            {
                maxId = id;
            }
        }
        var features = new string[maxId + 1];
        foreach ((string feature, int id) in db)
        {
            features[id] = feature;
        }
        for (int i = 0; i < features.Length; i++)
        {
            features[i] ??= string.Empty;
        }
        return features.ToList();
    }
}
