using System.Text;
using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class LatticeTests
{
    [Fact]
    public void SetKey_InitializesBosEos()
    {
        var lattice = new Lattice();
        lattice.SetKey("あい"); // UTF-8 で 6 バイト
        Assert.Equal(6, lattice.KeyLength);
        Assert.True(lattice.HasLattice);

        Assert.Equal(Node.NodeType.BosNode, lattice.BosNode.Type);
        Assert.Equal("BOS", lattice.BosNode.Value);
        Assert.Equal(Node.NodeType.EosNode, lattice.EosNode.Type);
        Assert.Equal("EOS", lattice.EosNode.Value);

        // BOS は end_nodes[0]、EOS は begin_nodes[keyLen]。
        Assert.Single(lattice.EndNodes(0));
        Assert.Single(lattice.BeginNodes(6));
    }

    [Fact]
    public void Insert_SetsBeginEndPositions()
    {
        var lattice = new Lattice();
        lattice.SetKey("あいう"); // 9 バイト

        var node = new Node { Key = "あい", Value = "藍" }; // key=6 バイト
        lattice.Insert(0, node);

        Assert.Equal(0, node.BeginPos);
        Assert.Equal(6, node.EndPos);
        Assert.Contains(node, lattice.BeginNodes(0));
        Assert.Contains(node, lattice.EndNodes(6));
    }

    [Fact]
    public void Insert_ClampsEndPosToKeyLength()
    {
        var lattice = new Lattice();
        lattice.SetKey("あ"); // 3 バイト

        var node = new Node { Key = "あいう", Value = "X" }; // 9 バイト(key より長い)
        lattice.Insert(0, node);
        Assert.Equal(3, node.EndPos); // key 長にクランプ。
    }

    [Fact]
    public void BosId_OverridesRid()
    {
        var lattice = new Lattice();
        lattice.SetKey("か", bosId: 42);
        Assert.Equal(42, lattice.BosNode.Rid);
    }

    [Fact]
    public void ScopedNodeInserter_InsertsOnDispose()
    {
        var lattice = new Lattice();
        lattice.SetKey("あい");

        var n = new Node { Key = "あ", Value = "亜" };
        using (var inserter = new Lattice.ScopedNodeInserter(lattice))
        {
            // 反復中の挿入を予約(まだ反映されない)。
            foreach (var _ in lattice.BeginNodes(0)) { }
            inserter.Insert(0, n);
            Assert.True(inserter.IsInserted);
            Assert.DoesNotContain(n, lattice.BeginNodes(0));
        }
        // Dispose 後に反映。
        Assert.Contains(n, lattice.BeginNodes(0));
        Assert.Equal(3, n.EndPos);
    }
}
