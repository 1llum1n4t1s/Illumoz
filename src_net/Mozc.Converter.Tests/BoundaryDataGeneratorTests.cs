using System.Buffers.Binary;
using Mozc.Converter;
using Xunit;

namespace Mozc.Converter.Tests;

public class BoundaryDataGeneratorTests
{
    private static ushort U16(byte[] b, int idx) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(idx * 2));

    [Fact]
    public void Generate_PrefixSuffixPenalties_AtCorrectOffsets()
    {
        var (prefix, suffix) = BoundaryDataGenerator.ParsePatterns(new[]
        {
            "PREFIX 名詞,接尾,              1000",
            "PREFIX 助詞,(格助詞|連体化)    3000",
            "SUFFIX 名詞,形容動詞語幹,      300",
        });

        var features = new List<string>
        {
            "BOS/EOS,*",          // id0
            "名詞,接尾,助数詞",     // id1 → prefix 1000
            "助詞,格助詞,一般",     // id2 → prefix 3000
            "名詞,形容動詞語幹,一般", // id3 → suffix 300
        };

        byte[] data = BoundaryDataGenerator.Generate(features, numSpecialPos: 1, prefix, suffix);

        Assert.Equal((features.Count + 1) * 4, data.Length); // 2*uint16 per id, +1 special

        // [2*id]=prefix, [2*id+1]=suffix
        Assert.Equal(0, U16(data, 0));      // id0 prefix
        Assert.Equal(0, U16(data, 1));      // id0 suffix
        Assert.Equal(1000, U16(data, 2));   // id1 prefix
        Assert.Equal(0, U16(data, 3));      // id1 suffix
        Assert.Equal(3000, U16(data, 4));   // id2 prefix
        Assert.Equal(0, U16(data, 5));      // id2 suffix
        Assert.Equal(0, U16(data, 6));      // id3 prefix
        Assert.Equal(300, U16(data, 7));    // id3 suffix
        // special pos(id4)は 0,0
        Assert.Equal(0, U16(data, 8));
        Assert.Equal(0, U16(data, 9));
    }

    [Fact]
    public void Generate_ReadableBySegmenterPenalties()
    {
        var (prefix, suffix) = BoundaryDataGenerator.ParsePatterns(new[]
        {
            "PREFIX 名詞,接尾, 1200",
            "SUFFIX 動詞,自立, 500",
        });
        var features = new List<string> { "BOS,*", "名詞,接尾,一般", "動詞,自立,基本形" };
        byte[] data = BoundaryDataGenerator.Generate(features, 0, prefix, suffix);

        var boundary = new ushort[data.Length / 2];
        for (int i = 0; i < boundary.Length; i++)
        {
            boundary[i] = U16(data, i);
        }
        int n = features.Count;
        var seg = new Segmenter(n, n, new ushort[n], new ushort[n], new byte[(n * n + 7) / 8], boundary);

        Assert.Equal(1200, seg.GetPrefixPenalty(1)); // 名詞,接尾,
        Assert.Equal(500, seg.GetSuffixPenalty(2));   // 動詞,自立,
        Assert.Equal(0, seg.GetPrefixPenalty(2));
    }

    [Fact]
    public void FeaturesById_OrdersByIdWithGaps()
    {
        var db = new List<(string, int)> { ("名詞", 2), ("BOS", 0) };
        List<string> f = BoundaryDataGenerator.FeaturesById(db);
        Assert.Equal(3, f.Count);
        Assert.Equal("BOS", f[0]);
        Assert.Equal("", f[1]); // gap
        Assert.Equal("名詞", f[2]);
    }
}
