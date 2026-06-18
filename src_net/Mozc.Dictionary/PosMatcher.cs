using System.Buffers.Binary;

namespace Mozc.Dictionary;

// C++ src/dictionary/pos_matcher.h 相当(データ駆動部)。
// data レイアウト(uint16 配列):
//   [0, lidTableSize)              : 各規則の関数ID(GetXxxId が返す lid)
//   [lidTableSize + ruleIndex]     : その規則のレンジ表への offset(uint16 単位)
//   レンジ表: (lo, hi) ペアの並び、0xFFFF 終端。id が lo<=id<=hi なら一致。
// 名前付きアクセサ(IsFunctional 等)は pos_matcher_rule.def 由来の ruleIndex を
// C6(データ生成)で別途マッピングする。ここは汎用エンジン。
public sealed class PosMatcher
{
    private readonly ushort[] _data;
    private readonly int _lidTableSize;

    public PosMatcher(ushort[] data, int lidTableSize)
    {
        _data = data;
        _lidTableSize = lidTableSize;
    }

    // pos_matcher.data (uint16 LE バイト列) から構築。
    public static PosMatcher FromBytes(ReadOnlySpan<byte> bytes, int lidTableSize)
    {
        var data = new ushort[bytes.Length / 2];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i * 2));
        }
        return new PosMatcher(data, lidTableSize);
    }

    // 規則 index の関数ID(C++ の GetXxxId 相当)。
    public ushort GetId(int ruleIndex) => _data[ruleIndex];

    // id が規則 ruleIndex のレンジ表に含まれるか(C++ IsRuleInTable 相当)。
    public bool IsRuleInTable(int ruleIndex, ushort id)
    {
        int offset = _data[_lidTableSize + ruleIndex];
        for (int p = offset; _data[p] != 0xFFFF; p += 2)
        {
            if (id >= _data[p] && id <= _data[p + 1])
            {
                return true;
            }
        }
        return false;
    }
}
