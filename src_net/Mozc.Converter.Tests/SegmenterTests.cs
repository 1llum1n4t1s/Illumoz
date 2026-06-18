using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class SegmenterTests
{
    [Fact]
    public void IsBoundary_MatchesMatrix_AndPenalties()
    {
        var rng = new Random(7);
        int n = 10;

        // l_table/r_table を恒等にし、bitarray_index = rid + n*lid とする。
        ushort[] lTable = new ushort[n];
        ushort[] rTable = new ushort[n];
        for (int i = 0; i < n; i++) { lTable[i] = (ushort)i; rTable[i] = (ushort)i; }

        bool[,] boundary = new bool[n, n];
        byte[] bitarray = new byte[(n * n + 7) / 8];
        for (int rid = 0; rid < n; rid++)
        {
            for (int lid = 0; lid < n; lid++)
            {
                bool b = rng.Next(2) == 0;
                boundary[rid, lid] = b;
                if (b)
                {
                    int idx = rid + n * lid;
                    bitarray[idx >> 3] |= (byte)(1 << (idx & 7));
                }
            }
        }

        ushort[] boundaryData = new ushort[2 * n];
        for (int i = 0; i < n; i++)
        {
            boundaryData[2 * i] = (ushort)(100 + i);     // prefix penalty for lid=i
            boundaryData[2 * i + 1] = (ushort)(200 + i); // suffix penalty for rid=i
        }

        var seg = new Segmenter(n, n, lTable, rTable, bitarray, boundaryData);

        for (int rid = 0; rid < n; rid++)
        {
            for (int lid = 0; lid < n; lid++)
            {
                Assert.Equal(boundary[rid, lid], seg.IsBoundary(rid, lid));
            }
            Assert.Equal(200 + rid, seg.GetSuffixPenalty(rid));
            Assert.Equal(100 + rid, seg.GetPrefixPenalty(rid));
        }
    }

    [Fact]
    public void Constructor_RejectsTooSmallBitarray()
    {
        Assert.Throws<ArgumentException>(() =>
            new Segmenter(10, 10, new ushort[10], new ushort[10], new byte[1], new ushort[20]));
    }
}
