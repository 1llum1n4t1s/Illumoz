namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/key_expansion_table.h 相当。
// キー文字(0..255)ごとに「展開先バイト集合」を 256bit のビットセットで保持。
// あいまい検索(濁点展開など)で使用。既定は恒等(各キーは自分自身のみ)。

// table_[key] (8 個の uint32 = 256bit) への読み取りビュー。
public readonly struct ExpandedKey
{
    private readonly uint[] _data;
    private readonly int _offset; // key * 8

    public ExpandedKey(uint[] data, int offset)
    {
        _data = data;
        _offset = offset;
    }

    public bool IsHit(byte value) => ((_data[_offset + value / 32] >> (value % 32)) & 1) != 0;
}

public sealed class KeyExpansionTable
{
    // [key*8 + value/32] のフラット配列(256 * 8)。
    private readonly uint[] _table = new uint[256 * 8];

    public KeyExpansionTable()
    {
        for (int i = 0; i < 256; i++)
        {
            SetBit((byte)i, (byte)i); // 恒等
        }
    }

    public void Add(byte key, ReadOnlySpan<byte> data)
    {
        foreach (byte v in data)
        {
            SetBit(key, v);
        }
    }

    public ExpandedKey ExpandKey(byte key) => new(_table, key * 8);

    private void SetBit(byte key, byte value)
    {
        _table[key * 8 + value / 32] |= 1u << (value % 32);
    }

    // 既定インスタンス(恒等のみ)。C++ GetDefaultInstance 相当。
    public static KeyExpansionTable Default { get; } = new();
}
