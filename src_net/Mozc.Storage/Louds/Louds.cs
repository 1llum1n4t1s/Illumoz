namespace Mozc.Storage.Louds;

// LOUDS 木のノード。C++ Louds::Node 相当(node_id は 1-based, root=1)。
// 注意: 必ず new LoudsNode() で生成すること(default(LoudsNode) は NodeId=0 で不正)。
public struct LoudsNode
{
    public int EdgeIndex;
    public int NodeId;

    public LoudsNode()
    {
        EdgeIndex = 0;
        NodeId = 1; // root
    }
}

// C++ src/storage/louds/louds.{h,cc} 相当。LOUDS ビット列で木を表現。
// select キャッシュは加速用のため Select0/Select1 直接呼び出しで同一結果。
public sealed class Louds
{
    private readonly SuccinctBitVectorIndex _index = new(); // chunk_size=32(既定)

    public void Init(byte[] image, int offset, int length) => _index.Init(image, offset, length);

    public static bool IsRoot(in LoudsNode node) => node.NodeId == 1;

    public void InitNodeFromNodeId(int nodeId, ref LoudsNode node)
    {
        node.NodeId = nodeId;
        node.EdgeIndex = _index.Select1(nodeId);
    }

    public void MoveToFirstChild(ref LoudsNode node)
    {
        node.EdgeIndex = _index.Select0(node.NodeId) + 1;
        node.NodeId = node.EdgeIndex - node.NodeId + 1;
    }

    public static void MoveToNextSibling(ref LoudsNode node)
    {
        node.EdgeIndex++;
        node.NodeId++;
    }

    public void MoveToParent(ref LoudsNode node)
    {
        node.NodeId = node.EdgeIndex - node.NodeId + 1;
        node.EdgeIndex = _index.Select1(node.NodeId);
    }

    public bool IsValidNode(in LoudsNode node) => _index.Get(node.EdgeIndex) != 0;
}
