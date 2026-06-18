using System.Buffers.Binary;
using Mozc.Base;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Mozc.Storage;
using Xunit;

namespace Mozc.Converter.Tests;

// C5/C6 capstone: 各生成器の出力を mozc.data(DataSet)に梱包 → DataManager で読み戻し、
// SystemDictionary/Connector/PosMatcher/Segmenter が全て動作することを検証する。
public class DataManagerTests
{
    private static byte[] ToBytes(ushort[] values)
    {
        var b = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i * 2), values[i]);
        }
        return b;
    }

    [Fact]
    public void Pack_DataManager_ReadsAllSections()
    {
        // --- dict ---
        byte[] dict = new SystemDictionaryBuilder().BuildDictionaryImage(new[]
        {
            new Token("とうきょう", "東京", 1234, 1, 1),
            new Token("あめ", "雨", 500, 2, 2),
        });

        // --- conn(pos_size=3, special=0)---
        byte[] conn = ConnectionDataGenerator.Generate(
            new[] { "3", "0", "640", "128", "128", "256", "192", "320", "384", "448" }, 0);

        // --- pos_matcher(35規則: 規則0=Functional が P0 にマッチ)---
        var posRules = new List<string> { "Functional ^P0" };
        for (int i = 1; i < PosMatcher.RuleCount; i++)
        {
            posRules.Add($"R{i} ^Q{i}");
        }
        var posDb = PosMatcherDataGenerator.ParsePosDatabase(new[] { "0 BOS/EOS", "1 P0,x" });
        ushort[] posData = PosMatcherDataGenerator.Generate(posDb, PosMatcherDataGenerator.ParseRules(posRules));

        // --- segmenter(小規模 db+規則)---
        var segDb = new List<(string, int)> { ("BOS/EOS,*", 0), ("名詞,一般", 1), ("助詞,格助詞", 2) };
        var segRules = SegmenterRuleMatcher.ParseRules(new[] { "名詞,一般 助詞,格助詞 false" });
        var isBoundary = SegmenterRuleMatcher.BuildIsBoundary(segDb, segRules);
        int segN = SegmenterRuleMatcher.PosCount(segDb);
        SegmenterBitarrayGenerator.Result seg = SegmenterBitarrayGenerator.Generate(segN, segN, isBoundary);

        // --- DataSet 梱包 ---
        var writer = new DataSetWriter(MozcConstants.DataSetMagicOss);
        writer.Add("dict", 32, dict);
        writer.Add("conn", 32, conn);
        writer.Add("pos_matcher", 32, ToBytes(posData));
        writer.Add("segmenter_sizeinfo", 32,
            DataManager.SerializeSegmenterSizeInfo(seg.CompressedLSize, seg.CompressedRSize));
        writer.Add("segmenter_ltable", 32, ToBytes(seg.LTable));
        writer.Add("segmenter_rtable", 32, ToBytes(seg.RTable));
        writer.Add("segmenter_bitarray", 32, seg.Bitarray);
        writer.Add("bdry", 32, ToBytes(new ushort[2 * (segN + 1)]));
        byte[] mozcData = writer.Finish();

        // --- DataManager で読み戻し ---
        var dm = new DataManager(mozcData);

        SystemDictionary sysdict = dm.GetSystemDictionary();
        Assert.True(sysdict.HasKey("とうきょう"));
        Assert.True(sysdict.HasValue("雨"));

        Connector connector = dm.GetConnector();
        // matrix[rid][lid], idx=rid*3+lid。[0][0]=0(強制), [0][1]=640, [0][2]=128。
        Assert.Equal(0, connector.GetTransitionCost(0, 0));
        Assert.Equal(640, connector.GetTransitionCost(0, 1));
        Assert.Equal(128, connector.GetTransitionCost(0, 2));

        PosMatcher pm = dm.GetPosMatcher();
        Assert.True(pm.IsFunctional(1));   // 規則0 が P0,x(id1)にマッチ
        Assert.False(pm.IsFunctional(0));

        Segmenter segmenter = dm.GetSegmenter();
        Assert.False(segmenter.IsBoundary(1, 2)); // 名詞→助詞 = false
        Assert.True(segmenter.IsBoundary(2, 2));  // default true
    }
}
