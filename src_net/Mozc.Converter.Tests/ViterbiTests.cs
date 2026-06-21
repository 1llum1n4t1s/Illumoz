using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

// 単一セグメント Viterbi の最小コスト経路をハンド計算と突き合わせて検証。
public class ViterbiTests
{
    // rsize=lsize=4, resolution=1(2byte 厳密値)。default=9999、指定遷移のみ低コスト。
    private static Connector MakeConnector(params (int Rid, int Lid, ushort Cost)[] transitions)
    {
        const int n = 4;
        var defaultCost = new ushort[n];
        for (int i = 0; i < n; i++) defaultCost[i] = 9999;
        var cost = new ushort[n][];
        for (int r = 0; r < n; r++)
        {
            cost[r] = new ushort[n];
            for (int l = 0; l < n; l++) cost[r][l] = 9999;
        }
        foreach ((int rid, int lid, ushort c) in transitions)
        {
            cost[rid][lid] = c;
        }
        return Connector.Create(ConnectorBuilder.Build(1, defaultCost, cost));
    }

    private static Node W(string key, string value, ushort lid, ushort rid, int wcost)
        => new() { Key = key, Value = value, Lid = lid, Rid = rid, Wcost = wcost };

    [Fact]
    public void SingleNodePath_ComputesCost()
    {
        // BOS(rid0)->A(lid1,rid1,wcost100)->EOS(lid0)
        var conn = MakeConnector((0, 1, 10), (1, 0, 10));
        var lattice = new Lattice();
        lattice.SetKey("abc"); // 3 バイト
        Node a = W("abc", "A", 1, 1, 100);
        lattice.Insert(0, a);

        var v = new Viterbi(conn);
        Assert.True(v.Forward(lattice));

        // a.cost = bos(0) + tc(0,1)=10 + wcost100 = 110
        Assert.Equal(110, a.Cost);
        // eos.cost = 110 + tc(1,0)=10 + 0 = 120
        Assert.Equal(120, lattice.EosNode.Cost);

        List<Node> path = Viterbi.BestPath(lattice);
        Assert.Single(path);
        Assert.Equal("A", path[0].Value);
    }

    [Fact]
    public void TwoPaths_ChoosesMinimumCost_SingleWins()
    {
        // A(一語): 0+10+100=110, eos=110+10=120
        // B+C(二語): B=0+10+50=60, C=60+5+50=115, eos=115+10=125
        // → A が勝つ。
        var conn = MakeConnector((0, 1, 10), (1, 0, 10), (0, 2, 10), (2, 3, 5), (3, 0, 10));
        var lattice = new Lattice();
        lattice.SetKey("abc");
        Node a = W("abc", "A", 1, 1, 100);
        Node b = W("ab", "B", 2, 2, 50);
        Node c = W("c", "C", 3, 3, 50);
        lattice.Insert(0, a);
        lattice.Insert(0, b);
        lattice.Insert(2, c);

        Assert.True(new Viterbi(conn).Forward(lattice));
        Assert.Equal(120, lattice.EosNode.Cost);
        List<Node> path = Viterbi.BestPath(lattice);
        Assert.Single(path);
        Assert.Equal("A", path[0].Value);
    }

    [Fact]
    public void TwoPaths_ChoosesMinimumCost_SplitWins()
    {
        // A の wcost を上げて B+C(125)が A(高)に勝つようにする。
        // A=0+10+200=210, eos=210+10=220 ; B+C eos=125 → B+C が勝つ。
        var conn = MakeConnector((0, 1, 10), (1, 0, 10), (0, 2, 10), (2, 3, 5), (3, 0, 10));
        var lattice = new Lattice();
        lattice.SetKey("abc");
        Node a = W("abc", "A", 1, 1, 200);
        Node b = W("ab", "B", 2, 2, 50);
        Node c = W("c", "C", 3, 3, 50);
        lattice.Insert(0, a);
        lattice.Insert(0, b);
        lattice.Insert(2, c);

        Assert.True(new Viterbi(conn).Forward(lattice));
        Assert.Equal(125, lattice.EosNode.Cost);
        List<Node> path = Viterbi.BestPath(lattice);
        Assert.Equal(2, path.Count);
        Assert.Equal("B", path[0].Value);
        Assert.Equal("C", path[1].Value);
        // prev/next リンクの確認。
        Assert.Equal("C", b.Next!.Value);
        Assert.Same(b, c.Prev);
    }
}
