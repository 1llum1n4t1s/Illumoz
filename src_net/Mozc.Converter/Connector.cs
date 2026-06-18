using System.Buffers.Binary;
using Mozc.Storage.Louds;

namespace Mozc.Converter;

// C++ src/converter/connector.{h,cc} 相当。連接コスト行列(疎圧縮)。
// レイアウト: Metadata(8B: magic 0xCDAB, resolution, rsize, lsize / 各 uint16 LE)
//   + default_cost[uint16 × DefaultCostArraySize(=rsize 偶数化)]
//   + 各行 rsize 個: [compact_bits_size(u16)][values_size(u16)]
//        [chunk_bits(ChunkBitsSize)][compact_bits(compact_bits_size)][values(values_size)]
// 行内アクセスは chunk_bits / compact_bits の二段 succinct。
// cost = value * resolution。1byte 値(resolution!=1)時、255 は無効(=kInvalidCost)。
public sealed class Connector
{
    public const int InvalidCost = 30000;
    private const ushort MagicNumber = 0xCDAB;
    private const byte Invalid1ByteCostValue = 255;

    private byte[] _data = Array.Empty<byte>();
    private int _resolution;
    private int _rsize;
    private int _defaultCostOffset; // default_cost uint16 配列のバイト位置
    private Row[] _rows = Array.Empty<Row>();

    public int Resolution => _resolution;

    public static Connector Create(byte[] connectionData)
    {
        var c = new Connector();
        c.Init(connectionData);
        return c;
    }

    private void Init(byte[] data)
    {
        if (data.Length < 8)
        {
            throw new ArgumentException("connection data too small");
        }
        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0));
        int resolution = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        int rsize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        int lsize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
        if (magic != MagicNumber)
        {
            throw new ArgumentException($"connector: bad magic 0x{magic:X4}");
        }
        if (rsize != lsize)
        {
            throw new ArgumentException("connector: matrix is not square");
        }

        _data = data;
        _resolution = resolution;
        _rsize = rsize;

        int numChunkBits = (lsize + 7) / 8;
        int chunkBitsSize = (numChunkBits + 31) / 32 * 4;
        bool use1Byte = resolution != 1;
        int defaultCostArraySize = rsize + (rsize & 1);

        int ptr = 8;
        _defaultCostOffset = ptr;
        ptr += defaultCostArraySize * 2;

        _rows = new Row[rsize];
        for (int i = 0; i < rsize; i++)
        {
            int compactBitsSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(ptr));
            ptr += 2;
            int valuesSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(ptr));
            ptr += 2;

            var chunkBits = new SuccinctBitVectorIndex();
            chunkBits.Init(data, ptr, chunkBitsSize);
            ptr += chunkBitsSize;

            var compactBits = new SuccinctBitVectorIndex();
            compactBits.Init(data, ptr, compactBitsSize);
            ptr += compactBitsSize;

            int valuesOffset = ptr;
            ptr += valuesSize;

            _rows[i] = new Row(chunkBits, compactBits, valuesOffset, use1Byte);
        }
    }

    public int GetTransitionCost(int rid, int lid) => LookupCost(rid, lid);

    private int LookupCost(int rid, int lid)
    {
        ushort? value = _rows[rid].GetValue(_data, (ushort)lid);
        if (value is null)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_defaultCostOffset + rid * 2));
        }
        return value.Value * _resolution;
    }

    private sealed class Row
    {
        private readonly SuccinctBitVectorIndex _chunkBits;
        private readonly SuccinctBitVectorIndex _compactBits;
        private readonly int _valuesOffset;
        private readonly bool _use1Byte;

        public Row(SuccinctBitVectorIndex chunkBits, SuccinctBitVectorIndex compactBits,
            int valuesOffset, bool use1Byte)
        {
            _chunkBits = chunkBits;
            _compactBits = compactBits;
            _valuesOffset = valuesOffset;
            _use1Byte = use1Byte;
        }

        public ushort? GetValue(byte[] data, ushort index)
        {
            int chunkBitPosition = index / 8;
            if (_chunkBits.Get(chunkBitPosition) == 0)
            {
                return null;
            }
            int compactBitPosition = _chunkBits.Rank1(chunkBitPosition) * 8 + index % 8;
            if (_compactBits.Get(compactBitPosition) == 0)
            {
                return null;
            }
            int valuePosition = _compactBits.Rank1(compactBitPosition);
            if (_use1Byte)
            {
                byte v = data[_valuesOffset + valuePosition];
                return v == Invalid1ByteCostValue ? (ushort)InvalidCost : v;
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(_valuesOffset + valuePosition * 2));
        }
    }
}
