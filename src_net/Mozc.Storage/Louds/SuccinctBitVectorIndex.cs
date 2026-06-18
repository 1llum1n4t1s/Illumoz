using System.Buffers.Binary;
using System.Numerics;

namespace Mozc.Storage.Louds;

// C++ src/storage/louds/simple_succinct_bit_vector_index.{h,cc} 相当。
// bit は LSB-first(bit i = data[i/8]>>(i%8) & 1)、word load は little-endian、
// chunk_size はバイト単位(既定32)、length はバイトで 4 の倍数。
// index_[k] = 先頭 k チャンク(= k*chunk_size バイト)内の 1 ビット数。
// rank/select は C++ と同一結果(lb キャッシュは加速用なので index_ 二分探索で代替)。
public sealed class SuccinctBitVectorIndex
{
    private byte[] _data = Array.Empty<byte>();
    private int _length;        // バイト数
    private readonly int _chunkSize; // バイト
    private int[] _index = Array.Empty<int>();

    public SuccinctBitVectorIndex(int chunkSize = 32)
    {
        if (chunkSize < 4 || !BitOperations.IsPow2(chunkSize))
        {
            throw new ArgumentException("chunk size must be power of 2 and >= 4", nameof(chunkSize));
        }
        _chunkSize = chunkSize;
    }

    public void Init(byte[] data, int length)
    {
        if (length % 4 != 0)
        {
            throw new ArgumentException("length must be a multiple of 4", nameof(length));
        }
        _data = data;
        _length = length;
        BuildIndex();
    }

    public int Get(int index) => (_data[index / 8] >> (index % 8)) & 1;
    public int GetNum1Bits() => _index[^1];
    public int GetNum0Bits() => 8 * _length - _index[^1];

    // [0, n) の 1 ビット数。
    public int Rank1(int n)
    {
        int numChunks = n / (_chunkSize * 8);
        int result = _index[numChunks];
        result += Count1Bits(numChunks * _chunkSize, (n / 8 - numChunks * _chunkSize) / 4);
        if (n % 32 > 0)
        {
            int offset = 4 * (n / 32);
            int shift = 32 - n % 32;
            result += BitOperations.PopCount(LoadU32(offset) << shift);
        }
        return result;
    }

    public int Rank0(int n) => n - Rank1(n);

    // n 番目(1-indexed)の 1 ビットの位置(0-indexed)。
    public int Select1(int n)
    {
        int chunkIndex = LowerBoundOnes(n) - 1;
        n -= _index[chunkIndex];
        int ptr = (chunkIndex * _chunkSize) & ~3;
        while (true)
        {
            int bc = BitOperations.PopCount(LoadU32(ptr));
            if (bc >= n) break;
            n -= bc;
            ptr += 4;
        }
        int index = ptr * 8;
        uint word = LoadU32(ptr);
        while (n > 0)
        {
            n -= (int)(word & 1);
            word >>= 1;
            ++index;
        }
        return index - 1;
    }

    // n 番目(1-indexed)の 0 ビットの位置(0-indexed)。
    public int Select0(int n)
    {
        int chunkIndex = LowerBoundZeros(n) - 1;
        n -= _chunkSize * 8 * chunkIndex - _index[chunkIndex];
        int ptr = (chunkIndex * _chunkSize) & ~3;
        while (true)
        {
            int bc = BitOperations.PopCount(~LoadU32(ptr));
            if (bc >= n) break;
            n -= bc;
            ptr += 4;
        }
        int index = ptr * 8;
        uint word = ~LoadU32(ptr);
        while (n > 0)
        {
            n -= (int)(word & 1);
            word >>= 1;
            ++index;
        }
        return index - 1;
    }

    private uint LoadU32(int byteOffset)
        => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(byteOffset, 4));

    private int Count1Bits(int byteOffset, int numWords)
    {
        int n = 0;
        for (int i = 0; i < numWords; i++)
        {
            n += BitOperations.PopCount(LoadU32(byteOffset));
            byteOffset += 4;
        }
        return n;
    }

    private void BuildIndex()
    {
        int chunkLength = (_length + _chunkSize - 1) / _chunkSize;
        _index = new int[chunkLength + 1];
        int numBits = 0;
        int idx = 0;
        int dataOff = 0;
        for (int remainingWords = _length / 4; remainingWords > 0;
             dataOff += _chunkSize, remainingWords -= _chunkSize / 4)
        {
            _index[idx++] = numBits;
            numBits += Count1Bits(dataOff, Math.Min(_chunkSize / 4, remainingWords));
        }
        _index[idx] = numBits;
    }

    // _index に対する lower_bound: _index[k] >= n となる最小 k。
    private int LowerBoundOnes(int n)
    {
        int lo = 0, hi = _index.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_index[mid] < n) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    // 0 ビット累積 zeros(k)=chunk_size*8*k - _index[k] に対する lower_bound。
    private int LowerBoundZeros(int n)
    {
        int lo = 0, hi = _index.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            int zeros = _chunkSize * 8 * mid - _index[mid];
            if (zeros < n) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
