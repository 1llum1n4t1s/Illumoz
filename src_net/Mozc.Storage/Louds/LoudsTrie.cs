using System.Buffers.Binary;

namespace Mozc.Storage.Louds;

// C++ src/storage/louds/louds_trie.{h,cc} 相当。
// 画像レイアウト(ヘッダ16B, 各 uint32 LE):
//   [0]louds_size [4]terminal_size [8]num_character_bits(=8) [12]edge_character_size
//   続いて louds(louds_size) / terminal(terminal_size) / edge_character(edge_character_size)
// キーは UTF-8 バイト列。edge ラベルはバイト。
public sealed class LoudsTrie
{
    public const int MaxDepth = 256;

    private readonly Louds _louds = new();
    private readonly SuccinctBitVectorIndex _terminal = new();
    private byte[] _image = Array.Empty<byte>();
    private int _edgeCharOffset;

    public bool Open(byte[] image)
    {
        int loudsSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(0));
        int terminalSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(4));
        int numCharBits = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(8));
        int edgeCharSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(12));
        if (numCharBits != 8 || edgeCharSize <= 0)
        {
            return false;
        }

        int loudsOffset = 16;
        int terminalOffset = loudsOffset + loudsSize;
        _edgeCharOffset = terminalOffset + terminalSize;

        _image = image;
        _louds.Init(image, loudsOffset, loudsSize);
        _terminal.Init(image, terminalOffset, terminalSize);
        return true;
    }

    public static LoudsNode Root => new();

    public bool IsValidNode(in LoudsNode node) => _louds.IsValidNode(node);

    public bool IsTerminalNode(in LoudsNode node) => _terminal.Get(node.NodeId - 1) != 0;

    public byte GetEdgeLabelToParentNode(in LoudsNode node) => _image[_edgeCharOffset + node.NodeId - 1];

    public int GetKeyIdOfTerminalNode(in LoudsNode node) => _terminal.Rank1(node.NodeId - 1);

    public void GetTerminalNodeFromKeyId(int keyId, ref LoudsNode node)
    {
        int nodeId = _terminal.Select1(keyId + 1) + 1;
        _louds.InitNodeFromNodeId(nodeId, ref node);
    }

    public void MoveToFirstChild(ref LoudsNode node) => _louds.MoveToFirstChild(ref node);

    public static void MoveToNextSibling(ref LoudsNode node) => Louds.MoveToNextSibling(ref node);

    public bool MoveToChildByLabel(byte label, ref LoudsNode node)
    {
        _louds.MoveToFirstChild(ref node);
        while (_louds.IsValidNode(node))
        {
            if (GetEdgeLabelToParentNode(node) == label)
            {
                return true;
            }
            Louds.MoveToNextSibling(ref node);
        }
        return false;
    }

    public bool Traverse(ReadOnlySpan<byte> key, ref LoudsNode node)
    {
        foreach (byte b in key)
        {
            if (!MoveToChildByLabel(b, ref node))
            {
                return false;
            }
        }
        return true;
    }

    public bool HasKey(ReadOnlySpan<byte> key)
    {
        var node = new LoudsNode();
        return Traverse(key, ref node) && IsTerminalNode(node);
    }

    // 一致検索。キーが存在し終端なら key id(0-based)、無ければ -1。
    public int ExactSearch(ReadOnlySpan<byte> key)
    {
        var node = new LoudsNode();
        if (Traverse(key, ref node) && IsTerminalNode(node))
        {
            return GetKeyIdOfTerminalNode(node);
        }
        return -1;
    }

    // 前方一致検索。key の各前置が終端ノードなら (prefixLength, keyId) を返す。
    public IEnumerable<(int PrefixLength, int KeyId)> PrefixSearch(byte[] key)
    {
        var node = new LoudsNode();
        var results = new List<(int, int)>();
        for (int i = 0; i < key.Length;)
        {
            if (!MoveToChildByLabel(key[i], ref node))
            {
                break;
            }
            i++;
            if (IsTerminalNode(node))
            {
                results.Add((i, GetKeyIdOfTerminalNode(node)));
            }
        }
        return results;
    }

    // 予測検索: prefix を前置に持つ全キー(prefix 自身が終端ならそれも含む)を
    // (key, keyId) で列挙する。C++ の LoudsTrie 予測走査相当(DFS)。
    public IEnumerable<(byte[] Key, int KeyId)> PredictiveSearch(byte[] prefix)
    {
        var start = new LoudsNode();
        if (!Traverse(prefix, ref start))
        {
            yield break;
        }

        var results = new List<(byte[], int)>();
        var current = new List<byte>(prefix);
        CollectDescendants(start, current, results);
        foreach (var r in results)
        {
            yield return r;
        }
    }

    private void CollectDescendants(LoudsNode node, List<byte> current, List<(byte[], int)> results)
    {
        if (IsTerminalNode(node))
        {
            results.Add((current.ToArray(), GetKeyIdOfTerminalNode(node)));
        }
        var child = node;
        _louds.MoveToFirstChild(ref child);
        while (_louds.IsValidNode(child))
        {
            byte label = GetEdgeLabelToParentNode(child);
            current.Add(label);
            CollectDescendants(child, current, results);
            current.RemoveAt(current.Count - 1);
            Louds.MoveToNextSibling(ref child);
        }
    }

    // key id から元のキー文字列(UTF-8 バイト)を復元。
    public byte[] RestoreKeyBytes(int keyId)
    {
        if (keyId < 0)
        {
            return Array.Empty<byte>();
        }
        var node = new LoudsNode();
        GetTerminalNodeFromKeyId(keyId, ref node);

        Span<byte> buf = stackalloc byte[MaxDepth];
        int pos = MaxDepth;
        while (!Louds.IsRoot(node))
        {
            buf[--pos] = GetEdgeLabelToParentNode(node);
            _louds.MoveToParent(ref node);
        }
        return buf.Slice(pos).ToArray();
    }
}
