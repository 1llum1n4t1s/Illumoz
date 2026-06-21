using System.Buffers.Binary;
using Mozc.Base;

namespace Mozc.Dictionary.File;

// C++ src/dictionary/file/codec.cc DictionaryFileCodec 相当(節の読み書き)。
// 形式: [filemagic:int32 LE=20110701][seed:int32 LE]
//        各節 [data_size:int32 LE][fingerprint:8byte][data:RoundUp4 パディング]
//        data_size==0 が終端マーカ。
// 節名→8byte fingerprint の対応(GetSectionName=LegacyFingerprintWithSeed ハッシュ)は
// 別途(バイト互換ハッシュの移植が必要)。ここは節バイナリの読み書きのみ。
public sealed class DictionaryFileSection
{
    public byte[] Fingerprint { get; }     // 8 バイト
    public ReadOnlyMemory<byte> Image { get; }

    public DictionaryFileSection(byte[] fingerprint, ReadOnlyMemory<byte> image)
    {
        if (fingerprint.Length != 8)
        {
            throw new ArgumentException("fingerprint must be 8 bytes", nameof(fingerprint));
        }
        Fingerprint = fingerprint;
        Image = image;
    }
}

public sealed class DictionaryFileCodec
{
    public const int FileMagic = 20110701;
    public const int DefaultSeed = 2135654146;
    private const int FingerprintLength = 8;

    public int Seed { get; private set; } = DefaultSeed;

    // C++ GetSectionName 相当。節名→8byte fingerprint(LegacyFingerprintWithSeed の
    // uint64 を little-endian 直列化)。
    public byte[] GetSectionName(string name)
    {
        ulong fp = LegacyFingerprint.FingerprintWithSeed(name, (uint)Seed);
        byte[] b = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, fp);
        return b;
    }

    // 論理節名で節を引く(fingerprint 照合)。
    public DictionaryFileSection? FindSection(IEnumerable<DictionaryFileSection> sections, string name)
    {
        byte[] fp = GetSectionName(name);
        foreach (DictionaryFileSection s in sections)
        {
            if (s.Fingerprint.AsSpan().SequenceEqual(fp))
            {
                return s;
            }
        }
        return null;
    }

    public List<DictionaryFileSection> ReadSections(byte[] image)
    {
        if (image.Length < 12)
        {
            throw new InvalidDataException($"insufficient data size: {image.Length}");
        }
        int p = 0;
        int magic = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(p));
        p += 4;
        if (magic != FileMagic)
        {
            throw new InvalidDataException($"invalid dictionary file magic: {magic}");
        }
        Seed = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(p));
        p += 4;

        var sections = new List<DictionaryFileSection>();
        for (; ; )
        {
            if (image.Length - p < 4)
            {
                throw new InvalidDataException("insufficient image to read data_size");
            }
            int dataSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(p));
            p += 4;
            if (dataSize == 0)
            {
                break; // 終端マーカ
            }
            int padded = RoundUp4(dataSize);
            if (p + FingerprintLength + padded > image.Length)
            {
                throw new InvalidDataException("section passes end of image");
            }
            byte[] fp = image.AsSpan(p, FingerprintLength).ToArray();
            p += FingerprintLength;
            var img = image.AsMemory(p, dataSize);
            sections.Add(new DictionaryFileSection(fp, img));
            p += padded;
        }
        if (p != image.Length)
        {
            throw new InvalidDataException($"{image.Length - p} bytes remaining");
        }
        return sections;
    }

    public byte[] WriteSections(IReadOnlyList<DictionaryFileSection> sections, int? seed = null)
    {
        var buf = new List<byte>();
        WriteInt32(buf, FileMagic);
        WriteInt32(buf, seed ?? Seed);

        // C++: 4 節のときは {0,2,1,3} の順で書く(レイアウト互換)。読み取りは fingerprint で引くため順序非依存。
        if (sections.Count == 4)
        {
            foreach (int i in new[] { 0, 2, 1, 3 })
            {
                WriteSection(buf, sections[i]);
            }
        }
        else
        {
            foreach (DictionaryFileSection s in sections)
            {
                WriteSection(buf, s);
            }
        }
        WriteInt32(buf, 0); // 終端
        return buf.ToArray();
    }

    private static void WriteSection(List<byte> buf, DictionaryFileSection s)
    {
        int size = s.Image.Length;
        WriteInt32(buf, size);
        buf.AddRange(s.Fingerprint);
        buf.AddRange(s.Image.ToArray());
        int pad = (4 - size % 4) % 4;
        for (int i = 0; i < pad; i++)
        {
            buf.Add(0);
        }
    }

    private static void WriteInt32(List<byte> buf, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        buf.Add(b[0]);
        buf.Add(b[1]);
        buf.Add(b[2]);
        buf.Add(b[3]);
    }

    private static int RoundUp4(int length) => length + (4 - length % 4) % 4;
}
