namespace Mozc.Converter;

// C6 データ生成: gen_connection_data.py 相当。
// connection_single_column.txt(1行目=pos_size, 続いて N*N 個のコスト1行ずつ)を
// 連接行列に展開し、各行の最頻コストを default として ConnectorBuilder で圧縮する。
// array_index=rid*N+lid → matrix[rid][lid](ConnectorBuilder の cost[rid][lid] と一致)。
public static class ConnectionDataGenerator
{
    public const int InvalidCost = 30000;
    public const int ResolutionFor1Byte = 64;

    // テキストを連接行列(mat_size = pos_size + special_pos_size)へ。
    public static ushort[][] ParseMatrix(IEnumerable<string> lines, int specialPosSize, out int posSize)
    {
        using IEnumerator<string> e = NonComment(lines).GetEnumerator();
        if (!e.MoveNext())
        {
            throw new FormatException("empty connection file");
        }
        posSize = int.Parse(e.Current.Trim());
        int matSize = posSize + specialPosSize;

        var matrix = new ushort[matSize][];
        for (int i = 0; i < matSize; i++)
        {
            matrix[i] = new ushort[matSize];
        }

        long total = (long)posSize * posSize;
        long idx = 0;
        while (idx < total && e.MoveNext())
        {
            int cost = int.Parse(e.Current.Trim());
            int rid = (int)(idx / posSize);
            int lid = (int)(idx % posSize);
            if (rid == 0 && lid == 0)
            {
                cost = 0;
            }
            matrix[rid][lid] = (ushort)cost;
            idx++;
        }
        if (idx != total)
        {
            throw new FormatException($"connection file truncated: {idx}/{total}");
        }

        // special pos 行(EOS lid=0 はスキップ)。
        for (int rid = posSize; rid < matSize; rid++)
        {
            for (int lid = 1; lid < matSize; lid++)
            {
                matrix[rid][lid] = InvalidCost;
            }
        }
        // special pos 列(BOS rid=0 はスキップ)。
        for (int lid = posSize; lid < matSize; lid++)
        {
            for (int rid = 1; rid < matSize; rid++)
            {
                matrix[rid][lid] = InvalidCost;
            }
        }
        return matrix;
    }

    // 各行の最頻コスト(INVALID 除く, 同数は最小コスト)を default に。
    public static ushort[] ComputeModeDefaults(ushort[][] matrix)
    {
        var defaults = new ushort[matrix.Length];
        for (int rid = 0; rid < matrix.Length; rid++)
        {
            var counts = new Dictionary<ushort, int>();
            foreach (ushort cost in matrix[rid])
            {
                if (cost == InvalidCost)
                {
                    continue;
                }
                counts[cost] = counts.GetValueOrDefault(cost) + 1;
            }
            ushort mode = 0;
            int best = -1;
            foreach (KeyValuePair<ushort, int> kv in counts)
            {
                if (kv.Value > best || (kv.Value == best && kv.Key < mode))
                {
                    best = kv.Value;
                    mode = kv.Key;
                }
            }
            defaults[rid] = mode;
        }
        return defaults;
    }

    // 連接データ画像(Connector が読む形式)を生成。
    public static byte[] Generate(IEnumerable<string> lines, int specialPosSize)
    {
        ushort[][] matrix = ParseMatrix(lines, specialPosSize, out _);
        ushort[] defaults = ComputeModeDefaults(matrix);
        return ConnectorBuilder.Build(ResolutionFor1Byte, defaults, matrix);
    }

    private static IEnumerable<string> NonComment(IEnumerable<string> lines)
    {
        foreach (string raw in lines)
        {
            string t = raw.TrimEnd('\r', '\n');
            if (t.TrimStart().StartsWith('#') || t.Trim().Length == 0)
            {
                continue;
            }
            yield return t;
        }
    }
}
