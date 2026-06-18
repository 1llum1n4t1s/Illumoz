using System.Buffers.Binary;
using System.Text;

namespace Mozc.Storage;

// C++ src/base/container/serialized_string_array.{h,cc} 相当。
// バイナリ形式(全 little-endian uint32):
//   [0]            : 要素数 count
//   [1+2i], [2+2i] : 文字列 i の (offset, length)  (i=0..count-1)
//   その後         : 各文字列(UTF-8) + '\0' 終端。全体は 4 バイト境界に整列。
// 読み取りは ReadOnlyMemory<byte> のゼロコピービュー。
public sealed class SerializedStringArray
{
    private ReadOnlyMemory<byte> _data;

    public int Count { get; private set; }

    public bool Init(ReadOnlyMemory<byte> data)
    {
        if (!VerifyData(data.Span))
        {
            _data = default;
            Count = 0;
            return false;
        }
        _data = data;
        Count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Span);
        return true;
    }

    // index 番目の文字列の生バイト(UTF-8)を返す。
    public ReadOnlySpan<byte> GetBytes(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        ReadOnlySpan<byte> span = _data.Span;
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4 + index * 8));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4 + index * 8 + 4));
        return span.Slice((int)offset, (int)length);
    }

    // index 番目の文字列(UTF-8 デコード)。
    public string GetString(int index) => Encoding.UTF8.GetString(GetBytes(index));

    public IEnumerable<string> AsEnumerable()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return GetString(i);
        }
    }

    // C++ VerifyData 相当。サイズ・offset 単調性の最小検証。
    public static bool VerifyData(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            return false;
        }
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(data);
        long minRequired = 4L + 8L * size;
        if (data.Length < minRequired)
        {
            return false;
        }

        long prevEnd = minRequired;
        for (int i = 0; i < size; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 + i * 8));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 + i * 8 + 4));
            if (offset < prevEnd)
            {
                return false;
            }
            long end = (long)offset + length;
            if (end > data.Length)
            {
                return false;
            }
            // 文字列の後には '\0' 終端が必要。
            if (end >= data.Length || data[(int)end] != 0)
            {
                return false;
            }
            prevEnd = end + 1;
        }
        return true;
    }

    // C++ SerializeToBuffer 相当。文字列群をバイナリ化(C6 データ生成でも再利用)。
    public static byte[] Build(IReadOnlyList<string> strings)
    {
        int count = strings.Count;
        int headerBytes = 4 * (1 + 2 * count);

        byte[][] utf8 = new byte[count][];
        int current = headerBytes;
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            utf8[i] = Encoding.UTF8.GetBytes(strings[i]);
            offsets[i] = current;
            current += utf8[i].Length + 1; // '\0' 終端分 +1
        }

        int totalBytes = (current + 3) & ~3; // 4 バイト境界に整列
        byte[] buffer = new byte[totalBytes];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)count);
        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4 + i * 8), (uint)offsets[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4 + i * 8 + 4), (uint)utf8[i].Length);
            utf8[i].CopyTo(buffer.AsSpan(offsets[i]));
            // '\0' 終端は配列初期値 0 のまま。
        }
        return buffer;
    }
}
