using System.Buffers.Binary;
using System.Text;
using Google.Protobuf;

namespace Mozc.Storage;

// C++ src/data_manager/dataset_reader.cc 相当。mozc.data パックを読む。
// レイアウト: [magic][各チャンク(整列あり)][metadata(DataSetMetadata)][footer]
//   footer(36B) = metadata_size(uint64 BE, 8) + SHA1(20) + filesize(uint64 BE, 8)
// uint64 は big-endian(Util::SerializeUint64)。チェックサムは検証しない(C++ も未検証)。
public sealed class DataSetReader
{
    private const int FooterSize = 36; // metadata_size(8) + sha1(20) + filesize(8)

    private ReadOnlyMemory<byte> _memblock;
    private readonly Dictionary<string, (int Offset, int Size)> _map = new();

    public IReadOnlyCollection<string> Names => _map.Keys;

    public bool Init(ReadOnlyMemory<byte> memblock, ReadOnlySpan<byte> magic)
    {
        if (!memblock.Span.StartsWith(magic))
        {
            return false;
        }
        return Init(memblock, magic.Length);
    }

    public bool Init(ReadOnlyMemory<byte> memblock, int magicLength)
    {
        _map.Clear();
        ReadOnlySpan<byte> span = memblock.Span;
        int size = span.Length;

        if (size < magicLength + FooterSize)
        {
            return false;
        }

        ulong filesize = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(size - 8, 8));
        if (filesize != (ulong)size)
        {
            return false;
        }

        ulong metadataSize = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(size - FooterSize, 8));
        long contentAndMetadataSize = (long)size - magicLength - FooterSize;
        if (metadataSize == 0 || contentAndMetadataSize < (long)metadataSize)
        {
            return false;
        }

        int metadataOffset = (int)(size - FooterSize - (int)metadataSize);

        DataSetMetadata metadata;
        try
        {
            metadata = DataSetMetadata.Parser.ParseFrom(memblock.Slice(metadataOffset, (int)metadataSize).ToArray());
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }

        long prevChunkEnd = magicLength;
        foreach (DataSetMetadata.Types.Entry e in metadata.Entries)
        {
            long offset = (long)e.Offset;
            long sz = (long)e.Size;
            if (offset < prevChunkEnd || offset >= metadataOffset)
            {
                return false;
            }
            if (sz > metadataOffset || offset > metadataOffset - sz)
            {
                return false;
            }
            _map[e.Name] = ((int)offset, (int)sz);
            prevChunkEnd = offset + sz;
        }

        _memblock = memblock;
        return true;
    }

    // 指定名のチャンクを取得。存在しなければ false。
    public bool TryGet(string name, out ReadOnlyMemory<byte> chunk)
    {
        if (_map.TryGetValue(name, out var loc))
        {
            chunk = _memblock.Slice(loc.Offset, loc.Size);
            return true;
        }
        chunk = default;
        return false;
    }

    public ReadOnlyMemory<byte> Get(string name)
        => TryGet(name, out var chunk) ? chunk : throw new KeyNotFoundException($"dataset chunk not found: {name}");
}
