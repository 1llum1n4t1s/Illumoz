using System.Text;
using Mozc.Base;
using Mozc.Storage;
using Xunit;

namespace Mozc.Storage.Tests;

public class DataSetTests
{
    private static byte[] Magic => MozcConstants.DataSetMagicOss.ToArray();

    [Fact]
    public void Writer_ThenReader_RoundTripsChunks()
    {
        byte[] chunkA = Encoding.UTF8.GetBytes("hello dataset");
        byte[] chunkB = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        var writer = new DataSetWriter(Magic);
        writer.Add("alpha", 8, chunkA);
        writer.Add("beta", 32, chunkB);
        byte[] image = writer.Finish();

        var reader = new DataSetReader();
        Assert.True(reader.Init(image, Magic));

        Assert.True(reader.TryGet("alpha", out var a));
        Assert.Equal(chunkA, a.ToArray());
        Assert.True(reader.TryGet("beta", out var b));
        Assert.Equal(chunkB, b.ToArray());
        Assert.Contains("alpha", reader.Names);
        Assert.Contains("beta", reader.Names);
    }

    [Fact]
    public void Reader_RejectsWrongMagic()
    {
        var writer = new DataSetWriter(Magic);
        writer.Add("x", 8, new byte[] { 0 });
        byte[] image = writer.Finish();

        var reader = new DataSetReader();
        Assert.False(reader.Init(image, "wrongmagic"u8));
    }

    [Fact]
    public void Reader_RejectsTamperedFileSize()
    {
        var writer = new DataSetWriter(Magic);
        writer.Add("x", 8, new byte[] { 0, 1, 2 });
        byte[] image = writer.Finish();
        image[^1] ^= 0xFF; // 末尾 filesize を破壊

        var reader = new DataSetReader();
        Assert.False(reader.Init(image, Magic));
    }

    [Fact]
    public void Reader_MissingChunk_Throws()
    {
        var writer = new DataSetWriter(Magic);
        writer.Add("present", 8, new byte[] { 9 });
        byte[] image = writer.Finish();

        var reader = new DataSetReader();
        Assert.True(reader.Init(image, Magic));
        Assert.False(reader.TryGet("absent", out _));
        Assert.Throws<KeyNotFoundException>(() => reader.Get("absent"));
    }

    // DataSet に SerializedStringArray を載せて取り出す統合テスト。
    [Fact]
    public void DataSet_CarriesSerializedStringArray()
    {
        var strings = new[] { "あ", "い", "うえお" };
        byte[] arrayChunk = SerializedStringArray.Build(strings);

        var writer = new DataSetWriter(Magic);
        writer.Add("my_strings", 32, arrayChunk);
        byte[] image = writer.Finish();

        var reader = new DataSetReader();
        Assert.True(reader.Init(image, Magic));
        Assert.True(reader.TryGet("my_strings", out var chunk));

        var arr = new SerializedStringArray();
        Assert.True(arr.Init(chunk));
        Assert.Equal(strings, arr.AsEnumerable());
    }
}
