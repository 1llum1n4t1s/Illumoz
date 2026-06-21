using System.Buffers.Binary;

namespace Mozc.Storage.Louds;

// C++ src/storage/louds/bit_vector_based_array.{h,cc} 相当。
// 可変長要素配列。各要素は base_length + num_steps*step_length バイトに丸めて格納。
// ビットベクタは要素ごとに 0bit + (num_steps 個の)1bit、末尾に番兵 0bit。
// 画像: ヘッダ16B(index_length, base_length, step_length, 0) + index + data。
public sealed class BitVectorBasedArray
{
    private readonly SuccinctBitVectorIndex _index = new();
    private byte[] _image = Array.Empty<byte>();
    private int _dataOffset;
    private int _baseLength;
    private int _stepLength;

    public void Open(byte[] image)
    {
        int indexLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(0));
        _baseLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(4));
        _stepLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(8));
        // image[12..16) は予約(0)
        _image = image;
        _index.Init(image, 16, indexLength);
        _dataOffset = 16 + indexLength;
    }

    // index 番目の要素(格納長 = base + step*num_steps、末尾 '\0' パディングを含みうる)。
    public ReadOnlyMemory<byte> Get(int index)
    {
        int bitIndex = _index.Select0(index + 1);
        int dataIndex = _baseLength * index + _stepLength * _index.Rank1(bitIndex);
        int i = bitIndex + 1;
        while (_index.Get(i) != 0)
        {
            i++;
        }
        int length = _baseLength + _stepLength * (i - bitIndex - 1);
        return _image.AsMemory(_dataOffset + dataIndex, length);
    }
}
