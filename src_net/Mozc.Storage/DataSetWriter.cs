using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Google.Protobuf;

namespace Mozc.Storage;

// C++ src/data_manager/dataset_writer.cc 相当。mozc.data パックを生成(C6 データ生成で使用)。
// テストで Reader との往復にも使う。出力は C++ とバイト一致を目指す。
public sealed class DataSetWriter
{
    private readonly MemoryStream _image = new();
    private readonly DataSetMetadata _metadata = new();
    private readonly HashSet<string> _seenNames = new();

    public DataSetWriter(ReadOnlySpan<byte> magic)
    {
        _image.Write(magic); // image_ は magic 起点
    }

    // alignment は bit 指定(8/16/32/64...、power-of-2 かつ >=8)。
    public void Add(string name, int alignmentBits, ReadOnlySpan<byte> data)
    {
        if (!_seenNames.Add(name))
        {
            throw new InvalidOperationException($"{name} was already added");
        }
        AppendPadding(alignmentBits);
        _metadata.Entries.Add(new DataSetMetadata.Types.Entry
        {
            Name = name,
            Offset = (ulong)_image.Length,
            Size = (ulong)data.Length,
        });
        _image.Write(data);
    }

    // C++ Finish 相当。metadata→metadata_size(BE)→SHA1(20)→filesize(BE) を付与し全体を返す。
    public byte[] Finish()
    {
        byte[] meta = _metadata.ToByteArray();
        _image.Write(meta);

        Span<byte> u64 = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(u64, (ulong)meta.Length);
        _image.Write(u64);

        // SHA1 はここまでの image 全体に対して計算。
        byte[] current = _image.ToArray();
        byte[] sha1 = SHA1.HashData(current);
        _image.Write(sha1);

        // filesize は「この 8 バイトを書いた後」の最終サイズ = 現在長 + 8。
        BinaryPrimitives.WriteUInt64BigEndian(u64, (ulong)(_image.Length + 8));
        _image.Write(u64);

        return _image.ToArray();
    }

    private void AppendPadding(int alignmentBits)
    {
        if (alignmentBits < 8 || !BitOperations.IsPow2(alignmentBits))
        {
            throw new ArgumentException($"Invalid alignment: {alignmentBits}", nameof(alignmentBits));
        }
        int alignmentBytes = alignmentBits / 8;
        int rem = (int)(_image.Length % alignmentBytes);
        if (rem > 0)
        {
            for (int i = 0; i < alignmentBytes - rem; i++)
            {
                _image.WriteByte(0);
            }
        }
    }
}
