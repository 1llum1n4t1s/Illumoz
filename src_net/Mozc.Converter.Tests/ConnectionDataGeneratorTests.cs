using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class ConnectionDataGeneratorTests
{
    [Fact]
    public void ParseMatrix_LayoutAndSpecialPos()
    {
        // pos_size=2, special=1 → mat_size=3。値は array_index=rid*2+lid 順。
        string[] lines = { "2", "999", "128", "640", "256" };
        ushort[][] m = ConnectionDataGenerator.ParseMatrix(lines, 1, out int posSize);

        Assert.Equal(2, posSize);
        Assert.Equal(0, m[0][0]);     // [0][0] は強制 0(999 ではない)
        Assert.Equal(128, m[0][1]);
        Assert.Equal(640, m[1][0]);
        Assert.Equal(256, m[1][1]);

        // special pos 行(rid=2): lid>=1 は INVALID, lid=0(EOS)はスキップ→0。
        Assert.Equal(0, m[2][0]);
        Assert.Equal(ConnectionDataGenerator.InvalidCost, m[2][1]);
        // special pos 列(lid=2): rid>=1 は INVALID, rid=0(BOS)はスキップ→0。
        Assert.Equal(0, m[0][2]);
        Assert.Equal(ConnectionDataGenerator.InvalidCost, m[1][2]);
    }

    [Fact]
    public void ComputeModeDefaults_PicksMostFrequentNonInvalid()
    {
        var matrix = new ushort[][]
        {
            new ushort[] { 100, 100, 200, 30000 }, // 100 が最頻
            new ushort[] { 50, 60, 30000, 30000 }, // 同数(50,60)→最小 50
        };
        ushort[] defaults = ConnectionDataGenerator.ComputeModeDefaults(matrix);
        Assert.Equal(100, defaults[0]);
        Assert.Equal(50, defaults[1]);
    }

    [Fact]
    public void Generate_RoundTripsThroughConnector()
    {
        // 64 の倍数コストで exact、special セルは INVALID 復元。
        string[] lines = { "2", "0", "128", "640", "128" };
        byte[] image = ConnectionDataGenerator.Generate(lines, 1);
        var connector = Connector.Create(image);

        Assert.Equal(0, connector.GetTransitionCost(0, 0));
        Assert.Equal(128, connector.GetTransitionCost(0, 1));
        Assert.Equal(640, connector.GetTransitionCost(1, 0));
        Assert.Equal(128, connector.GetTransitionCost(1, 1));
        // special 列セル(通常行×special列)は INVALID 復元。
        Assert.Equal(ConnectionDataGenerator.InvalidCost, connector.GetTransitionCost(1, 2));
    }
}
