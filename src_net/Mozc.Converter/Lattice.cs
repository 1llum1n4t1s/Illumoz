using System.Text;

namespace Mozc.Converter;

// C++ src/converter/lattice.{h,cc} 相当。
// key(UTF-8)の各バイト位置で開始/終了するノード集合を保持するラティス。
// 位置はすべて UTF-8 バイトオフセット(辞書の前方一致がバイト境界で動くため)。
// C++ の NodeAllocator は C# では GC に委ね、Node(参照型)を都度生成する。
public sealed class Lattice
{
    private string _key = string.Empty;
    private int _keyLen; // UTF-8 バイト長
    private List<Node>[] _beginNodes = global::System.Array.Empty<List<Node>>();
    private List<Node>[] _endNodes = global::System.Array.Empty<List<Node>>();

    public string Key => _key;
    public int KeyLength => _keyLen;
    public bool HasLattice => _beginNodes.Length > 0;

    public static Node NewNode() => new();

    // key を設定し BOS/EOS でラティスを初期化。bos_id で左文脈 id を上書きできる。
    public void SetKey(string key, ushort bosId = 0)
    {
        _key = key;
        _keyLen = Encoding.UTF8.GetByteCount(key);

        _beginNodes = new List<Node>[_keyLen + 1];
        _endNodes = new List<Node>[_keyLen + 1];
        for (int i = 0; i <= _keyLen; i++)
        {
            _beginNodes[i] = new List<Node>();
            _endNodes[i] = new List<Node>();
        }

        Node bos = InitBosNode(NewNode(), 0, bosId);
        Node eos = InitEosNode(NewNode(), (ushort)_keyLen);
        _endNodes[0].Add(bos);
        _beginNodes[_keyLen].Add(eos);
    }

    public IReadOnlyList<Node> BeginNodes(int pos) => _beginNodes[pos];
    public IReadOnlyList<Node> EndNodes(int pos) => _endNodes[pos];

    public Node BosNode => _endNodes[0][0];
    public Node EosNode => _beginNodes[_keyLen][0];

    // 位置 pos に 1 ノードを挿入(begin/end pos を設定)。
    public void Insert(int pos, Node node)
    {
        int endPos = global::System.Math.Min(Encoding.UTF8.GetByteCount(node.Key) + pos, _keyLen);
        node.BeginPos = (ushort)pos;
        node.EndPos = (ushort)endPos;
        node.Prev = null;
        node.Next = null;
        node.Cost = 0;
        _beginNodes[pos].Add(node);
        _endNodes[endPos].Add(node);
    }

    public void Insert(int pos, IEnumerable<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            Insert(pos, node);
        }
    }

    // C++ ScopedLatticeNodeInserter 相当。反復中に挿入予約し、まとめて挿入する。
    public sealed class ScopedNodeInserter : IDisposable
    {
        private readonly Lattice _lattice;
        private readonly List<(int Pos, Node Node)> _inserted = new();

        public ScopedNodeInserter(Lattice lattice) => _lattice = lattice;
        public bool IsInserted => _inserted.Count > 0;
        public void Insert(int pos, Node node) => _inserted.Add((pos, node));

        public void Dispose()
        {
            foreach ((int pos, Node node) in _inserted)
            {
                _lattice.Insert(pos, node);
            }
        }
    }

    private static Node InitBosNode(Node bos, ushort position, ushort bosId)
    {
        bos.Rid = bosId; // 0 は EOS/BOS 予約。
        bos.Lid = 0;
        bos.Key = string.Empty;
        bos.Value = "BOS";
        bos.Type = Node.NodeType.BosNode;
        bos.Wcost = 0;
        bos.Cost = 0;
        bos.BeginPos = position;
        bos.EndPos = position;
        return bos;
    }

    private static Node InitEosNode(Node eos, ushort position)
    {
        eos.Rid = 0;
        eos.Lid = 0;
        eos.Key = string.Empty;
        eos.Value = "EOS";
        eos.Type = Node.NodeType.EosNode;
        eos.Wcost = 0;
        eos.Cost = 0;
        eos.BeginPos = position;
        eos.EndPos = position;
        return eos;
    }
}
