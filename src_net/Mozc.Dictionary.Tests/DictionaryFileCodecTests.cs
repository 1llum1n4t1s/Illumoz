using System.Text;
using Mozc.Dictionary.File;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class DictionaryFileCodecTests
{
    private static byte[] Fp(string s)
    {
        var fp = new byte[8];
        byte[] src = Encoding.ASCII.GetBytes(s);
        Array.Copy(src, fp, Math.Min(8, src.Length));
        return fp;
    }

    [Fact]
    public void WriteSections_ThenRead_RoundTrips()
    {
        var sections = new List<DictionaryFileSection>
        {
            new(Fp("key"), Encoding.UTF8.GetBytes("KEY-DATA")),
            new(Fp("value"), Encoding.UTF8.GetBytes("v")),         // 非4倍長(パディング検証)
            new(Fp("tokens"), new byte[] { 1, 2, 3, 4, 5 }),
        };

        var codec = new DictionaryFileCodec();
        byte[] image = codec.WriteSections(sections);

        var read = new DictionaryFileCodec().ReadSections(image);
        Assert.Equal(3, read.Count);

        // fingerprint で対応付け(順序非依存)。
        foreach (DictionaryFileSection original in sections)
        {
            DictionaryFileSection match = read.Single(r => r.Fingerprint.SequenceEqual(original.Fingerprint));
            Assert.Equal(original.Image.ToArray(), match.Image.ToArray());
        }
    }

    [Fact]
    public void FourSections_SpecialOrder_StillReadable()
    {
        var sections = new List<DictionaryFileSection>
        {
            new(Fp("s0"), new byte[] { 0 }),
            new(Fp("s1"), new byte[] { 1, 1 }),
            new(Fp("s2"), new byte[] { 2, 2, 2 }),
            new(Fp("s3"), new byte[] { 3, 3, 3, 3 }),
        };
        var codec = new DictionaryFileCodec();
        byte[] image = codec.WriteSections(sections); // {0,2,1,3} 順で書かれる
        var read = codec.ReadSections(image);

        Assert.Equal(4, read.Count);
        for (int i = 0; i < 4; i++)
        {
            DictionaryFileSection match = read.Single(r => r.Fingerprint.SequenceEqual(Fp("s" + i)));
            Assert.Equal(sections[i].Image.ToArray(), match.Image.ToArray());
        }
    }

    [Fact]
    public void SeedRoundTrips()
    {
        var codec = new DictionaryFileCodec();
        byte[] image = codec.WriteSections(new[] { new DictionaryFileSection(Fp("x"), new byte[] { 9 }) }, seed: 12345);
        var reader = new DictionaryFileCodec();
        reader.ReadSections(image);
        Assert.Equal(12345, reader.Seed);
    }

    [Fact]
    public void BadMagic_Throws()
    {
        byte[] bad = new byte[12];
        Assert.Throws<InvalidDataException>(() => new DictionaryFileCodec().ReadSections(bad));
    }
}
