using System.Buffers.Binary;
using Mozc.Base;
using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Mozc.Storage;

namespace Mozc.Converter;

// C6 capstone: src/data の各ソースを生成器に通し、mozc.data(DataSet)を C# だけで組み立てる。
// dataset_writer 相当。dict/conn/pos_matcher/segmenter_*/bdry を梱包する(変換中核セクション)。
// 予測・記号・絵文字等の追加セクションは後続。
public sealed class DataSetBuilder
{
    public sealed class Sources
    {
        public IEnumerable<string> DictionaryLines = global::System.Array.Empty<string>();
        public IEnumerable<string> ConnectionLines = global::System.Array.Empty<string>();
        public int ConnectionSpecialPosSize;
        public IEnumerable<string> IdDefLines = global::System.Array.Empty<string>();
        public IEnumerable<string> SpecialPosLines = global::System.Array.Empty<string>();
        public IEnumerable<string> PosMatcherRuleLines = global::System.Array.Empty<string>();
        public IEnumerable<string> SegmenterRuleLines = global::System.Array.Empty<string>();
        public IEnumerable<string> BoundaryDefLines = global::System.Array.Empty<string>();
    }

    private const int Align = 32;

    public byte[] Build(Sources s)
    {
        // id データベース(全 pos: id.def + special)。pos_matcher / segmenter で共用。
        var fullDb = PosMatcherDataGenerator.ParsePosDatabase(s.IdDefLines, s.SpecialPosLines);
        // id.def のみ(boundary の features 用)と special 件数。
        var idOnlyDb = PosMatcherDataGenerator.ParsePosDatabase(s.IdDefLines);
        int specialCount = fullDb.Count - idOnlyDb.Count;

        // dict
        var tokens = DictionaryTextParser.Parse(s.DictionaryLines);
        byte[] dict = new SystemDictionaryBuilder().BuildDictionaryImage(tokens);

        // conn
        byte[] conn = ConnectionDataGenerator.Generate(s.ConnectionLines, s.ConnectionSpecialPosSize);

        // pos_matcher
        var posRules = PosMatcherDataGenerator.ParseRules(s.PosMatcherRuleLines);
        ushort[] posData = PosMatcherDataGenerator.Generate(fullDb, posRules);

        // segmenter
        var segRules = SegmenterRuleMatcher.ParseRules(s.SegmenterRuleLines);
        Func<int, int, bool> isBoundary = SegmenterRuleMatcher.BuildIsBoundary(fullDb, segRules);
        int n = SegmenterRuleMatcher.PosCount(fullDb);
        SegmenterBitarrayGenerator.Result seg = SegmenterBitarrayGenerator.Generate(n, n, isBoundary);

        // bdry
        var (prefix, suffix) = BoundaryDataGenerator.ParsePatterns(s.BoundaryDefLines);
        byte[] bdry = BoundaryDataGenerator.Generate(
            BoundaryDataGenerator.FeaturesById(idOnlyDb), specialCount, prefix, suffix);

        // 梱包。
        var writer = new DataSetWriter(MozcConstants.DataSetMagicOss);
        writer.Add("dict", Align, dict);
        writer.Add("conn", Align, conn);
        writer.Add("pos_matcher", Align, ToBytes(posData));
        writer.Add("segmenter_sizeinfo", Align,
            DataManager.SerializeSegmenterSizeInfo(seg.CompressedLSize, seg.CompressedRSize));
        writer.Add("segmenter_ltable", Align, ToBytes(seg.LTable));
        writer.Add("segmenter_rtable", Align, ToBytes(seg.RTable));
        writer.Add("segmenter_bitarray", Align, seg.Bitarray);
        writer.Add("bdry", Align, bdry);
        return writer.Finish();
    }

    private static byte[] ToBytes(ushort[] values)
    {
        var b = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i * 2), values[i]);
        }
        return b;
    }
}
