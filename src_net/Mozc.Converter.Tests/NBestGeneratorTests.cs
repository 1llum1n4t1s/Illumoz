using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

// NBestGenerator(A* N-best 列挙)の検証。
// 単一セグメント・同音異義(雨/飴/亜目)を全長 1 ノードで表し、コスト昇順列挙を確認する。
public class NBestGeneratorTests
{
    private static Connector UniformConnector()
    {
        const int n = 4;
        var def = new ushort[n];
        for (int i = 0; i < n; i++) def[i] = 10;
        var cost = new ushort[n][];
        for (int r = 0; r < n; r++)
        {
            cost[r] = new ushort[n];
            for (int l = 0; l < n; l++) cost[r][l] = 10;
        }
        return Connector.Create(ConnectorBuilder.Build(1, def, cost));
    }

    // 全境界 false(エッジは Node 種別 BOS/EOS で true 扱い)。
    private static Segmenter AllFalseSegmenter()
    {
        const int n = 4;
        var lTable = new ushort[n];
        var rTable = new ushort[n];
        for (int i = 0; i < n; i++) { lTable[i] = (ushort)i; rTable[i] = (ushort)i; }
        var bitarray = new byte[(n * n + 7) / 8];
        var boundary = new ushort[2 * n];
        return new Segmenter(n, n, lTable, rTable, bitarray, boundary);
    }

    private static Node Word(string key, string value, ushort lid, ushort rid, int wcost)
        => new() { Key = key, Value = value, Lid = lid, Rid = rid, Wcost = wcost };

    // 全規則空レンジの PosMatcher を用いた CandidateFilter(重複除去のみ実効)。
    private static CandidateFilter EmptyFilter()
    {
        int n = PosMatcher.RuleCount;
        var data = new ushort[n + n + 1];
        data[n + n] = 0xFFFF;
        for (int i = 0; i < n; i++) data[n + i] = (ushort)(n + n);
        return new CandidateFilter(new PosMatcher(data, n));
    }

    [Fact]
    public void EnumeratesHomophones_InCostOrder()
    {
        Connector conn = UniformConnector();
        Segmenter seg = AllFalseSegmenter();

        var lattice = new Lattice();
        lattice.SetKey("あめ"); // 6 バイト
        // 同音 3 候補(全長 1 ノード)。wcost 昇順: 雨(100) < 飴(150) < 亜目(220)
        lattice.Insert(0, Word("あめ", "雨", 1, 1, 100));
        lattice.Insert(0, Word("あめ", "飴", 2, 2, 150));
        lattice.Insert(0, Word("あめ", "亜目", 3, 3, 220));

        Assert.True(new Viterbi(conn).Forward(lattice));

        var nbest = new NBestGenerator(conn, seg, lattice, _ => false, EmptyFilter());
        nbest.Reset(lattice.BosNode, lattice.EosNode);

        var seg2 = new Segment();
        seg2.SetKey("あめ");
        nbest.SetCandidates(seg2, 3);

        Assert.Equal(3, seg2.CandidatesSize);
        // コスト昇順(=wcost 昇順)に列挙される。
        Assert.Equal("雨", seg2.Get(0).Value);
        Assert.Equal("飴", seg2.Get(1).Value);
        Assert.Equal("亜目", seg2.Get(2).Value);

        // 先頭は Viterbi best。cost = EOS.cost。
        Assert.Equal(lattice.EosNode.Cost, seg2.Get(0).Cost);
        Assert.Equal(100, seg2.Get(0).Wcost);
        // content_key/value は全長(functional 無し)。
        Assert.Equal("あめ", seg2.Get(0).ContentKey);
        Assert.Equal("雨", seg2.Get(0).ContentValue);
    }

    [Fact]
    public void TopResult_IsViterbiBest()
    {
        Connector conn = UniformConnector();
        Segmenter seg = AllFalseSegmenter();
        var lattice = new Lattice();
        lattice.SetKey("あめ");
        lattice.Insert(0, Word("あめ", "飴", 2, 2, 150));
        lattice.Insert(0, Word("あめ", "雨", 1, 1, 100));
        Assert.True(new Viterbi(conn).Forward(lattice));

        var nbest = new NBestGenerator(conn, seg, lattice, _ => false, EmptyFilter());
        nbest.Reset(lattice.BosNode, lattice.EosNode);
        var s = new Segment();
        s.SetKey("あめ");
        nbest.SetCandidates(s, 1);

        Assert.Equal(1, s.CandidatesSize);
        Assert.Equal("雨", s.Get(0).Value); // 挿入順に依らず最小コストが先頭。
    }

    [Fact]
    public void SetCandidates_StopsWhenExhausted()
    {
        Connector conn = UniformConnector();
        Segmenter seg = AllFalseSegmenter();
        var lattice = new Lattice();
        lattice.SetKey("あめ");
        lattice.Insert(0, Word("あめ", "雨", 1, 1, 100));
        Assert.True(new Viterbi(conn).Forward(lattice));

        var nbest = new NBestGenerator(conn, seg, lattice, _ => false, EmptyFilter());
        nbest.Reset(lattice.BosNode, lattice.EosNode);
        var s = new Segment();
        s.SetKey("あめ");
        nbest.SetCandidates(s, 10); // 候補は 1 つしかない。

        Assert.Equal(1, s.CandidatesSize);
        Assert.Equal("雨", s.Get(0).Value);
    }
}
