using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class ConnectorTests
{
    [Theory]
    [InlineData(1)]    // 2 バイト値
    [InlineData(64)]   // 1 バイト値(cost/resolution)
    public void Build_ThenRead_MatchesDenseMatrix(int resolution)
    {
        var rng = new Random(424242 + resolution);
        int n = 20;
        ushort[] defaultCost = new ushort[n];
        ushort[][] cost = new ushort[n][];
        for (int rid = 0; rid < n; rid++)
        {
            defaultCost[rid] = (ushort)(rng.Next(0, 50) * resolution);
            cost[rid] = new ushort[n];
            for (int lid = 0; lid < n; lid++)
            {
                // 一部を default のまま(=非格納)、一部を別値に。
                cost[rid][lid] = rng.Next(3) == 0
                    ? defaultCost[rid]
                    : (ushort)(rng.Next(0, 200) * resolution);
            }
        }

        byte[] image = ConnectorBuilder.Build(resolution, defaultCost, cost);
        var connector = Connector.Create(image);

        Assert.Equal(resolution, connector.Resolution);
        for (int rid = 0; rid < n; rid++)
        {
            for (int lid = 0; lid < n; lid++)
            {
                Assert.Equal(cost[rid][lid], connector.GetTransitionCost(rid, lid));
            }
        }
    }

    [Fact]
    public void EmptyRows_ReturnDefault()
    {
        int n = 12;
        var defaultCost = new ushort[n];
        var cost = new ushort[n][];
        for (int rid = 0; rid < n; rid++)
        {
            defaultCost[rid] = (ushort)(1000 + rid);
            cost[rid] = new ushort[n];
            for (int lid = 0; lid < n; lid++)
            {
                cost[rid][lid] = defaultCost[rid]; // 全て default(=非格納)
            }
        }

        byte[] image = ConnectorBuilder.Build(1, defaultCost, cost);
        var connector = Connector.Create(image);
        for (int rid = 0; rid < n; rid++)
        {
            for (int lid = 0; lid < n; lid++)
            {
                Assert.Equal(defaultCost[rid], connector.GetTransitionCost(rid, lid));
            }
        }
    }

    [Fact]
    public void InvalidCost_In1ByteMode_RoundTrips()
    {
        // 1byte モード(resolution=64)で、行 default と異なる INVALID_COST セルが
        // 255 格納 → reader で 30000 に復元されること(連接行列の special 列セル相当)。
        const int n = 4;
        const int resolution = 64;
        var defaultCost = new ushort[n];
        var cost = new ushort[n][];
        for (int rid = 0; rid < n; rid++)
        {
            defaultCost[rid] = (ushort)(128); // 非INVALID の最頻値
            cost[rid] = new ushort[n];
            for (int lid = 0; lid < n; lid++)
            {
                cost[rid][lid] = 128;
            }
        }
        cost[1][2] = 30000; // INVALID, default(128)と異なる→stored
        cost[0][3] = 640;   // 通常 stored 値(64の倍数)

        byte[] image = ConnectorBuilder.Build(resolution, defaultCost, cost);
        var connector = Connector.Create(image);

        Assert.Equal(30000, connector.GetTransitionCost(1, 2)); // INVALID 復元
        Assert.Equal(640, connector.GetTransitionCost(0, 3));
        Assert.Equal(128, connector.GetTransitionCost(0, 0));   // default
    }

    [Fact]
    public void RejectsBadMagic()
    {
        byte[] bad = new byte[8];
        Assert.Throws<ArgumentException>(() => Connector.Create(bad));
    }
}
