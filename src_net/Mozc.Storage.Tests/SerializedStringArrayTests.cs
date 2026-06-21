using Mozc.Storage;
using Xunit;

namespace Mozc.Storage.Tests;

public class SerializedStringArrayTests
{
    [Fact]
    public void Build_ThenInit_RoundTrips()
    {
        var input = new[] { "とうきょう", "東京", "", "a", "𠮷野家" };
        byte[] data = SerializedStringArray.Build(input);

        var arr = new SerializedStringArray();
        Assert.True(arr.Init(data));
        Assert.Equal(input.Length, arr.Count);
        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(input[i], arr.GetString(i));
        }
        Assert.Equal(input, arr.AsEnumerable());
    }

    [Fact]
    public void EmptyArray_RoundTrips()
    {
        byte[] data = SerializedStringArray.Build(Array.Empty<string>());
        var arr = new SerializedStringArray();
        Assert.True(arr.Init(data));
        Assert.Equal(0, arr.Count);
    }

    // C++ serialized_string_array.cc と byte-for-byte 一致するレイアウトを固定するゴールデン。
    [Fact]
    public void Build_ProducesExpectedByteLayout()
    {
        byte[] data = SerializedStringArray.Build(new[] { "a", "bb" });

        // count=2; header=4*(1+2*2)=20; o0=20,len0=1; o1=22,len1=2; "a\0" "bb\0"; 25→pad 28
        byte[] expected =
        {
            0x02, 0x00, 0x00, 0x00, // count
            0x14, 0x00, 0x00, 0x00, // offset0 = 20
            0x01, 0x00, 0x00, 0x00, // length0 = 1
            0x16, 0x00, 0x00, 0x00, // offset1 = 22
            0x02, 0x00, 0x00, 0x00, // length1 = 2
            0x61, 0x00,             // "a\0"
            0x62, 0x62, 0x00,       // "bb\0"
            0x00, 0x00, 0x00,       // 4 バイト境界へのパディング(25→28)
        };
        Assert.Equal(expected, data);
    }

    [Fact]
    public void VerifyData_RejectsTooShort()
    {
        Assert.False(SerializedStringArray.VerifyData(new byte[] { 0x01, 0x00 }));
        Assert.False(SerializedStringArray.VerifyData(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Init_RejectsCorruptOffset()
    {
        // count=1 だが offset が範囲外
        byte[] bad =
        {
            0x01, 0x00, 0x00, 0x00,
            0xFF, 0x00, 0x00, 0x00, // offset = 255 (範囲外)
            0x01, 0x00, 0x00, 0x00,
        };
        var arr = new SerializedStringArray();
        Assert.False(arr.Init(bad));
    }
}
