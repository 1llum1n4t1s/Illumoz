using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class SegmenterBitarrayGeneratorTests
{
    // 生成器が内部で作る行列(番兵 + stride=lsize のエイリアス含む)を C++ と同手順で再現。
    private static byte[] BuildArray(int lsize, int rsize, Func<int, int, bool> isBoundary)
    {
        var array = new byte[(lsize + 1) * (rsize + 1)];
        for (int rid = 0; rid <= lsize; rid++)
        {
            for (int lid = 0; lid <= rsize; lid++)
            {
                int index = rid + lsize * lid;
                array[index] = (rid == lsize || lid == rsize)
                    ? (byte)1
                    : (byte)(isBoundary(rid, lid) ? 1 : 0);
            }
        }
        return array;
    }

    // 生成した l_table/r_table/bitarray を Segmenter で読み戻し、生成器の行列値と一致するか
    // (C++ の検証 CHECK_EQ(barray.get(cindex), array[index]!=0) と同じ契約)。
    private static void RoundTrip(int lsize, int rsize, Func<int, int, bool> isBoundary)
    {
        SegmenterBitarrayGenerator.Result r =
            SegmenterBitarrayGenerator.Generate(lsize, rsize, isBoundary);
        byte[] array = BuildArray(lsize, rsize, isBoundary);

        var seg = new Segmenter(r.CompressedLSize, r.CompressedRSize,
            r.LTable, r.RTable, r.Bitarray, new ushort[2 * (System.Math.Max(lsize, rsize) + 1)]);

        for (int rid = 0; rid < lsize; rid++)
        {
            for (int lid = 0; lid < rsize; lid++)
            {
                bool expected = array[rid + lsize * lid] != 0;
                Assert.Equal(expected, seg.IsBoundary(rid, lid));
            }
        }
    }

    [Fact]
    public void RoundTrip_RowDependentPattern()
    {
        // 偶数 rid のみ境界 → 行に重複(偶/奇)・列は全同一 → 圧縮が効く。
        RoundTrip(6, 6, (rid, lid) => rid % 2 == 0);
    }

    [Fact]
    public void RoundTrip_CheckerboardWithDups()
    {
        RoundTrip(8, 8, (rid, lid) => (rid / 2 + lid / 2) % 2 == 0);
    }

    [Fact]
    public void RoundTrip_Asymmetric()
    {
        RoundTrip(5, 9, (rid, lid) => (rid + lid) % 3 == 0);
    }

    [Fact]
    public void CompressesDuplicateRows()
    {
        // 全行同一(境界は lid のみ依存)→ compressed_lsize は小さく(2: 実データ行+番兵)。
        var r = SegmenterBitarrayGenerator.Generate(10, 10, (rid, lid) => lid % 2 == 0);
        Assert.True(r.CompressedLSize < 11); // 圧縮されている
        Assert.Equal(11, r.LTable.Length);   // lsize+1
        Assert.Equal(11, r.RTable.Length);
    }
}
