using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Converter.Tests;

// ImmutableConverter の会話変換スライス: 辞書→ラティス→Viterbi→セグメント分割+NBest。
public class ImmutableConverterTests
{
    private static Connector UniformConnector()
    {
        const int n = 6;
        var def = new ushort[n];
        for (int i = 0; i < n; i++) def[i] = 10;
        var cost = new ushort[n][];
        for (int r = 0; r < n; r++)
        {
            cost[r] = new ushort[n];
            for (int l = 0; l < n; l++) cost[r][l] = 10;
        }
        return Connector.Create(ConnectorBuilder.Build(1, def, cost));
    }

    // 指定 (rid,lid) ペアのみ境界 true(identity テーブル, index=rid+n*lid)。
    private static Segmenter SegmenterWithBoundaries(int n, params (int Rid, int Lid)[] boundaries)
    {
        var lTable = new ushort[n];
        var rTable = new ushort[n];
        for (int i = 0; i < n; i++) { lTable[i] = (ushort)i; rTable[i] = (ushort)i; }
        var bitarray = new byte[(n * n + 7) / 8];
        foreach ((int rid, int lid) in boundaries)
        {
            int idx = rid + n * lid;
            bitarray[idx >> 3] |= (byte)(1 << (idx & 7));
        }
        return new Segmenter(n, n, lTable, rTable, bitarray, new ushort[2 * n]);
    }

    private static PosMatcher EmptyPosMatcher()
    {
        int n = PosMatcher.RuleCount;
        var data = new ushort[n + n + 1];
        data[n + n] = 0xFFFF;
        for (int i = 0; i < n; i++) data[n + i] = (ushort)(n + n);
        return new PosMatcher(data, n);
    }

    [Fact]
    public void Convert_SplitsIntoThreeSegments()
    {
        // 辞書: 私/の/名前(重ならない3語)。
        var dict = new SystemDictionaryBuilder().Build(new[]
        {
            new Token("わたし", "私", 100, 1, 1),
            new Token("の", "の", 50, 2, 2),
            new Token("なまえ", "名前", 100, 3, 3),
        });
        Connector conn = UniformConnector();
        // 私|の と の|名前 で境界。
        Segmenter seg = SegmenterWithBoundaries(6, (1, 2), (2, 3));
        PosMatcher pos = EmptyPosMatcher();
        var filter = new CandidateFilter(pos);

        var conv = new ImmutableConverter(dict, conn, seg, pos, filter);
        Segments segments = conv.Convert("わたしのなまえ");

        Assert.Equal(3, segments.ConversionSegmentsSize);
        Assert.Equal("わたし", segments.ConversionSegment(0).Key);
        Assert.Equal("私", segments.ConversionSegment(0).Get(0).Value);
        Assert.Equal("の", segments.ConversionSegment(1).Get(0).Value);
        Assert.Equal("名前", segments.ConversionSegment(2).Get(0).Value);
    }

    [Fact]
    public void Convert_SingleSegment_WhenNoBoundary()
    {
        // 境界を一切設定しない → 全体が1セグメント。同音 雨/飴 が候補に並ぶ。
        var dict = new SystemDictionaryBuilder().Build(new[]
        {
            new Token("あめ", "雨", 100, 1, 1),
            new Token("あめ", "飴", 150, 1, 1),
        });
        Connector conn = UniformConnector();
        Segmenter seg = SegmenterWithBoundaries(6); // 境界なし
        PosMatcher pos = EmptyPosMatcher();

        var conv = new ImmutableConverter(dict, conn, seg, pos, new CandidateFilter(pos));
        Segments segments = conv.Convert("あめ");

        Assert.Equal(1, segments.ConversionSegmentsSize);
        Segment s = segments.ConversionSegment(0);
        Assert.Equal("あめ", s.Key);
        Assert.True(s.CandidatesSize >= 2);
        Assert.Equal("雨", s.Get(0).Value);   // 低コスト先頭
        Assert.Contains(s.Candidates, c => c.Value == "飴");
    }
}
