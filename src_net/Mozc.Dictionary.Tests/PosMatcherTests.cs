using System.Buffers.Binary;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class PosMatcherTests
{
    // lidTableSize=2 の合成データ:
    //  [0]=100(rule0 id) [1]=200(rule1 id) [2]=offset0=4 [3]=offset1=9
    //  rule0 ranges @4: (10,20)(30,30)0xFFFF
    //  rule1 ranges @9: (50,60)0xFFFF
    private static ushort[] BuildData() => new ushort[]
    {
        100, 200,        // 関数ID表
        4, 9,            // オフセット表
        10, 20, 30, 30, 0xFFFF, // rule0
        50, 60, 0xFFFF,         // rule1
    };

    [Fact]
    public void GetId_ReturnsFunctionalIds()
    {
        var m = new PosMatcher(BuildData(), lidTableSize: 2);
        Assert.Equal(100, m.GetId(0));
        Assert.Equal(200, m.GetId(1));
    }

    [Theory]
    [InlineData(0, 10, true)]
    [InlineData(0, 15, true)]
    [InlineData(0, 20, true)]
    [InlineData(0, 21, false)]
    [InlineData(0, 30, true)]
    [InlineData(0, 9, false)]
    [InlineData(1, 50, true)]
    [InlineData(1, 60, true)]
    [InlineData(1, 61, false)]
    [InlineData(1, 49, false)]
    public void IsRuleInTable_ChecksRanges(int rule, int id, bool expected)
    {
        var m = new PosMatcher(BuildData(), lidTableSize: 2);
        Assert.Equal(expected, m.IsRuleInTable(rule, (ushort)id));
    }

    // 35規則の合成データ。Functional(0)=[100,200], Pronoun(24)=[300,300]、他は空。
    private static PosMatcher BuildNamed()
    {
        int n = PosMatcher.RuleCount; // 35
        var data = new ushort[n + n + 7];
        // 関数ID表。
        data[(int)PosMatcher.Rule.Functional] = 10;
        data[(int)PosMatcher.Rule.Pronoun] = 24;
        data[(int)PosMatcher.Rule.Number] = 4;
        // レンジ表: 70=Functional, 73=Pronoun, 76=空(0xFFFF)。
        int rangeBase = n + n; // 70
        data[rangeBase + 0] = 100; data[rangeBase + 1] = 200; data[rangeBase + 2] = 0xFFFF;
        data[rangeBase + 3] = 300; data[rangeBase + 4] = 300; data[rangeBase + 5] = 0xFFFF;
        data[rangeBase + 6] = 0xFFFF; // 空レンジ。
        // オフセット表(既定は空レンジ 76)。
        for (int i = 0; i < n; i++) data[n + i] = (ushort)(rangeBase + 6);
        data[n + (int)PosMatcher.Rule.Functional] = (ushort)(rangeBase + 0);
        data[n + (int)PosMatcher.Rule.Pronoun] = (ushort)(rangeBase + 3);
        return new PosMatcher(data, lidTableSize: n);
    }

    [Fact]
    public void NamedAccessors_MapToRuleIndices()
    {
        var m = BuildNamed();
        Assert.Equal(10, m.GetFunctionalId());
        Assert.Equal(24, m.GetPronounId());

        Assert.True(m.IsFunctional(150));
        Assert.True(m.IsFunctional(100));
        Assert.True(m.IsFunctional(200));
        Assert.False(m.IsFunctional(201));

        Assert.True(m.IsPronoun(300));
        Assert.False(m.IsPronoun(301));

        // 空レンジの規則は常に false。
        Assert.False(m.IsContentNoun(150));
        Assert.False(m.IsVerbSuffix(150));
        Assert.False(m.IsKagyoTaConnectionVerb(150));
    }

    [Fact]
    public void FromBytes_ParsesLittleEndianUint16()
    {
        ushort[] data = BuildData();
        byte[] bytes = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), data[i]);
        }

        var m = PosMatcher.FromBytes(bytes, lidTableSize: 2);
        Assert.Equal(100, m.GetId(0));
        Assert.True(m.IsRuleInTable(0, 15));
        Assert.False(m.IsRuleInTable(1, 49));
    }
}
