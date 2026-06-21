using System.Text;
using Mozc.Storage.Louds;
using Xunit;

namespace Mozc.Storage.Tests;

public class BitVectorBasedArrayTests
{
    [Fact]
    public void RoundTrips_VariableLengthElements()
    {
        var elements = new[]
        {
            Encoding.UTF8.GetBytes("a"),
            Encoding.UTF8.GetBytes("東京"),       // 6 bytes
            Encoding.UTF8.GetBytes("hello world"),
            Array.Empty<byte>(),
            Encoding.UTF8.GetBytes("12"),
        };

        var builder = new BitVectorBasedArrayBuilder();
        builder.SetSize(baseLength: 2, stepLength: 4);
        foreach (byte[] e in elements)
        {
            builder.Add(e);
        }
        byte[] image = builder.Build();

        var array = new BitVectorBasedArray();
        array.Open(image);

        for (int i = 0; i < elements.Length; i++)
        {
            ReadOnlyMemory<byte> got = array.Get(i);
            // 格納長は base+step*k で element 以上。先頭に element がそのまま入り、残りは '\0'。
            Assert.True(got.Length >= elements[i].Length);
            Assert.Equal(elements[i], got.Span.Slice(0, elements[i].Length).ToArray());
            for (int p = elements[i].Length; p < got.Length; p++)
            {
                Assert.Equal(0, got.Span[p]);
            }
        }
    }

    [Fact]
    public void FixedRecordSize_NoPadding()
    {
        // base=4, step=任意。全要素 4 バイト固定 → パディングなし、length は常に 4。
        var elements = new[]
        {
            new byte[] { 1, 2, 3, 4 },
            new byte[] { 5, 6, 7, 8 },
            new byte[] { 9, 10, 11, 12 },
        };
        var builder = new BitVectorBasedArrayBuilder();
        builder.SetSize(baseLength: 4, stepLength: 4);
        foreach (var e in elements) builder.Add(e);
        byte[] image = builder.Build();

        var array = new BitVectorBasedArray();
        array.Open(image);
        for (int i = 0; i < elements.Length; i++)
        {
            Assert.Equal(elements[i], array.Get(i).ToArray());
        }
    }
}
