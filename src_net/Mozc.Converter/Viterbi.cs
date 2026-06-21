namespace Mozc.Converter;

// C++ src/converter/immutable_converter.cc の Viterbi / ViterbiInternal 相当(単一セグメント版)。
// ラティス上で BOS→各ノード→EOS の最小コスト経路を求め、prev/next/cost を確定する。
// セグメント境界制約・constrained_prev・予測用 high-speed 版は未対応(後続で Segments 移植時)。
//
// コスト: node.cost = min_{lnode} (lnode.cost + connector.GetTransitionCost(lnode.rid, node.lid)) + node.wcost
// 連接コストは Connector のみ(セグメンタのペナルティはノード wcost 側に織り込む設計)。
public sealed class Viterbi
{
    // INT_MAX>>2(新コスト計算で溢れないよう INT_MAX は使わない)。C++ kVeryBigCost。
    private const int VeryBigCost = int.MaxValue >> 2;

    private readonly Connector _connector;

    public Viterbi(Connector connector) => _connector = connector;

    // ラティス全体(単一セグメント)で Viterbi を実行。成功時 true。
    // 失敗(BOS まで辿れない)時 false。
    public bool Forward(Lattice lattice)
    {
        int keyLen = lattice.KeyLength;
        Node bos = lattice.BosNode;

        // BOS 処理: 位置 0 開始の各ノードを BOS に接続。
        foreach (Node rnode in lattice.BeginNodes(0))
        {
            rnode.Prev = bos;
            rnode.Cost = bos.Cost + _connector.GetTransitionCost(bos.Rid, rnode.Lid) + rnode.Wcost;
        }

        // 内部位置 1..keyLen-1 を順に処理。
        for (int pos = 1; pos < keyLen; pos++)
        {
            ViterbiInternal(pos, keyLen, lattice);
        }

        // EOS 処理: key 末尾で終わる最小コストノードを選ぶ。
        Node eos = lattice.EosNode;
        int bestCost = VeryBigCost;
        Node? bestNode = null;
        foreach (Node lnode in lattice.EndNodes(keyLen))
        {
            if (lnode.Prev == null)
            {
                continue; // 無効ノード。
            }
            int cost = lnode.Cost + _connector.GetTransitionCost(lnode.Rid, eos.Lid);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestNode = lnode;
            }
        }
        eos.Prev = bestNode;
        eos.Cost = bestCost + eos.Wcost;

        // 末尾から先頭へ next リンクを張る。
        Node node = eos;
        Node? prev = null;
        while (node.Prev != null)
        {
            prev = node.Prev;
            prev.Next = node;
            node = prev;
        }

        return ReferenceEquals(lattice.BosNode, prev);
    }

    private void ViterbiInternal(int pos, int rightBoundary, Lattice lattice)
    {
        foreach (Node rnode in lattice.BeginNodes(pos))
        {
            if (rnode.EndPos > rightBoundary)
            {
                rnode.Prev = null;
                continue;
            }

            int bestCost = VeryBigCost;
            Node? bestNode = null;
            foreach (Node lnode in lattice.EndNodes(pos))
            {
                if (lnode.Prev == null)
                {
                    continue; // 無効ノード。
                }
                int cost = lnode.Cost + _connector.GetTransitionCost(lnode.Rid, rnode.Lid);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestNode = lnode;
                }
            }
            rnode.Prev = bestNode;
            rnode.Cost = bestCost + rnode.Wcost;
        }
    }

    // 確定経路を BOS→EOS の順に列挙(BOS/EOS を除く実ノード列)。
    public static List<Node> BestPath(Lattice lattice)
    {
        var path = new List<Node>();
        for (Node? node = lattice.BosNode.Next; node != null && node.Type != Node.NodeType.EosNode; node = node.Next)
        {
            path.Add(node);
        }
        return path;
    }
}
