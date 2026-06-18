using System.Buffers.Binary;
using Mozc.Storage.Louds;

namespace Mozc.Converter;

// Connector の疎圧縮バイナリを生成する(テスト/将来の C6 データ生成で使用)。
// 密な cost 行列 + default cost から、default と異なるエントリのみを格納する。
public sealed class ConnectorBuilder
{
    private const ushort MagicNumber = 0xCDAB;

    // cost[rid][lid] と default[rid] から連接データを生成。
    // resolution!=1 のとき値は 1 バイト(cost/resolution、255=無効)、=1 のとき 2 バイト。
    public static byte[] Build(int resolution, ushort[] defaultCost, ushort[][] cost)
    {
        int rsize = defaultCost.Length;
        bool use1Byte = resolution != 1;
        int numChunkBits = (rsize + 7) / 8;

        var image = new List<byte>();
        // Metadata
        AppendU16(image, MagicNumber);
        AppendU16(image, (ushort)resolution);
        AppendU16(image, (ushort)rsize); // rsize
        AppendU16(image, (ushort)rsize); // lsize (== rsize)

        // default_cost(偶数化パディング)
        int defaultArraySize = rsize + (rsize & 1);
        for (int i = 0; i < defaultArraySize; i++)
        {
            AppendU16(image, i < rsize ? defaultCost[i] : (ushort)0);
        }

        for (int rid = 0; rid < rsize; rid++)
        {
            // 格納対象 lid = default と異なるもの。
            var stored = new List<int>();
            for (int lid = 0; lid < rsize; lid++)
            {
                if (cost[rid][lid] != defaultCost[rid])
                {
                    stored.Add(lid);
                }
            }
            var storedSet = new HashSet<int>(stored);

            // chunk_bits: lid/8 のチャンクが格納対象を含むか。
            var setChunks = new SortedSet<int>();
            foreach (int lid in stored) setChunks.Add(lid / 8);

            var chunkStream = new BitStream();
            for (int c = 0; c < numChunkBits; c++)
            {
                chunkStream.PushBit(setChunks.Contains(c) ? 1 : 0);
            }
            chunkStream.FillPadding32();

            // compact_bits + values: 設定チャンクを昇順に、各 8bit(lid 0..7)。
            var compactStream = new BitStream();
            var values = new List<byte>();
            foreach (int c in setChunks)
            {
                for (int b = 0; b < 8; b++)
                {
                    int lid = c * 8 + b;
                    bool present = lid < rsize && storedSet.Contains(lid);
                    compactStream.PushBit(present ? 1 : 0);
                    if (present)
                    {
                        int v = cost[rid][lid] / resolution;
                        if (use1Byte)
                        {
                            values.Add((byte)v);
                        }
                        else
                        {
                            values.Add((byte)(v & 0xFF));
                            values.Add((byte)((v >> 8) & 0xFF));
                        }
                    }
                }
            }
            compactStream.FillPadding32();
            while (values.Count % 4 != 0) values.Add(0); // 4 バイト境界

            var chunkBytes = new List<byte>();
            chunkStream.CopyTo(chunkBytes);
            var compactBytes = new List<byte>();
            compactStream.CopyTo(compactBytes);

            AppendU16(image, (ushort)compactBytes.Count); // compact_bits_size
            AppendU16(image, (ushort)values.Count);       // values_size
            image.AddRange(chunkBytes);
            image.AddRange(compactBytes);
            image.AddRange(values);
        }

        return image.ToArray();
    }

    private static void AppendU16(List<byte> list, ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        list.Add(b[0]);
        list.Add(b[1]);
    }
}
