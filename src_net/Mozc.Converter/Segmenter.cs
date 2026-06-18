namespace Mozc.Converter;

// C++ src/converter/segmenter.{h,cc} 相当(core)。POS id 対の分割境界判定。
// bitarray_index = l_table[rid] + l_num_elements * r_table[lid]、bit は LSB-first。
// ペナルティは boundary_data[2*lid](prefix) / boundary_data[2*rid+1](suffix)。
// 注: Node ベースの IsBoundary(lnode,rnode,...) overload は Node 移植後に追加。
public sealed class Segmenter
{
    private readonly int _lNumElements;
    private readonly ushort[] _lTable; // rid で参照
    private readonly ushort[] _rTable; // lid で参照
    private readonly byte[] _bitarray;
    private readonly ushort[] _boundary;

    public Segmenter(int lNumElements, int rNumElements,
        ushort[] lTable, ushort[] rTable, byte[] bitarray, ushort[] boundary)
    {
        if ((long)lNumElements * rNumElements > (long)bitarray.Length * 8)
        {
            throw new ArgumentException("bitarray too small for l_num*r_num");
        }
        _lNumElements = lNumElements;
        _lTable = lTable;
        _rTable = rTable;
        _bitarray = bitarray;
        _boundary = boundary;
    }

    // C++ Segmenter::IsBoundary(lnode, rnode, is_single_segment) 相当。
    public bool IsBoundary(Node lnode, Node rnode, bool isSingleSegment)
    {
        if (lnode.Type == Node.NodeType.BosNode || rnode.Type == Node.NodeType.EosNode)
        {
            return true;
        }
        if (isSingleSegment)
        {
            return false;
        }
        if ((lnode.Attributes & Node.Attribute.StartsWithParticle) != 0)
        {
            return false;
        }
        return IsBoundary(lnode.Rid, rnode.Lid);
    }

    public bool IsBoundary(int rid, int lid)
    {
        int index = _lTable[rid] + _lNumElements * _rTable[lid];
        return ((_bitarray[index >> 3] >> (index & 7)) & 1) != 0;
    }

    public int GetPrefixPenalty(int lid) => _boundary[2 * lid];

    public int GetSuffixPenalty(int rid) => _boundary[2 * rid + 1];
}
