using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class StringMapCodecTests
{
    [Fact]
    public void RoundTrip_PreservesEntries()
    {
        var map = new Dictionary<string, string[]>
        {
            ["やじるし"] = new[] { "→", "←", "↑" },
            ["ほし"] = new[] { "★", "☆" },
            ["えがお"] = new[] { "😀" },
        };
        byte[] bytes = StringMapCodec.Serialize(map);
        var back = StringMapCodec.Deserialize(bytes);

        Assert.Equal(3, back.Count);
        Assert.Equal(new[] { "→", "←", "↑" }, back["やじるし"]);
        Assert.Equal(new[] { "★", "☆" }, back["ほし"]);
        Assert.Equal(new[] { "😀" }, back["えがお"]);
    }

    [Fact]
    public void Deterministic_SameInputSameBytes()
    {
        var a = new Dictionary<string, string[]> { ["b"] = new[] { "2" }, ["a"] = new[] { "1" } };
        var b = new Dictionary<string, string[]> { ["a"] = new[] { "1" }, ["b"] = new[] { "2" } };
        Assert.Equal(StringMapCodec.Serialize(a), StringMapCodec.Serialize(b));
    }

    [Fact]
    public void Empty_RoundTrips()
    {
        byte[] bytes = StringMapCodec.Serialize(new Dictionary<string, string[]>());
        Assert.Empty(StringMapCodec.Deserialize(bytes));
    }
}
