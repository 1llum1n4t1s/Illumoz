using Mozc.Storage.Louds;
using Xunit;

namespace Mozc.Storage.Tests;

// rank/select を総当たり参照実装と突き合わせて検証する。
// C++ 出力が無くても、定義上の正しさ(Rank1=前方1ビット数, Select=n番目位置)を保証できる。
public class SuccinctBitVectorIndexTests
{
    private static int RefRank1(byte[] data, int n)
    {
        int c = 0;
        for (int i = 0; i < n; i++)
        {
            if (((data[i / 8] >> (i % 8)) & 1) == 1) c++;
        }
        return c;
    }

    private static int RefSelect(byte[] data, int totalBits, int n, int bit)
    {
        int seen = 0;
        for (int i = 0; i < totalBits; i++)
        {
            int b = (data[i / 8] >> (i % 8)) & 1;
            if (b == bit)
            {
                if (++seen == n) return i;
            }
        }
        return -1;
    }

    [Theory]
    [InlineData(4, 12345)]
    [InlineData(8, 222)]
    [InlineData(32, 99)]
    [InlineData(64, 7)]
    public void RankSelect_MatchReferenceOverRandomData(int chunkSize, int seed)
    {
        var rng = new Random(seed);
        int lengthBytes = 4 * rng.Next(1, 64); // 4 の倍数
        byte[] data = new byte[lengthBytes + 8]; // 末尾に余裕(word load の安全マージン)
        rng.NextBytes(data);
        int totalBits = lengthBytes * 8;

        var sbv = new SuccinctBitVectorIndex(chunkSize);
        sbv.Init(data, lengthBytes);

        // Get / Rank1 / Rank0
        for (int n = 0; n <= totalBits; n++)
        {
            int expected = RefRank1(data, n);
            Assert.Equal(expected, sbv.Rank1(n));
            Assert.Equal(n - expected, sbv.Rank0(n));
        }

        // Select1
        int num1 = sbv.GetNum1Bits();
        Assert.Equal(RefRank1(data, totalBits), num1);
        for (int k = 1; k <= num1; k++)
        {
            Assert.Equal(RefSelect(data, totalBits, k, 1), sbv.Select1(k));
        }

        // Select0
        int num0 = sbv.GetNum0Bits();
        for (int k = 1; k <= num0; k++)
        {
            Assert.Equal(RefSelect(data, totalBits, k, 0), sbv.Select0(k));
        }
    }

    [Fact]
    public void Get_ReturnsBitsLsbFirst()
    {
        byte[] data = new byte[4];
        data[0] = 0b0000_0101; // bit0=1, bit2=1
        var sbv = new SuccinctBitVectorIndex(4);
        sbv.Init(data, 4);
        Assert.Equal(1, sbv.Get(0));
        Assert.Equal(0, sbv.Get(1));
        Assert.Equal(1, sbv.Get(2));
        Assert.Equal(2, sbv.Rank1(8));
        Assert.Equal(0, sbv.Select1(1)); // 1番目の1ビットは位置0
        Assert.Equal(2, sbv.Select1(2)); // 2番目は位置2
    }
}
