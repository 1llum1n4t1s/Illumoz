using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class KeyExpansionTableTests
{
    [Fact]
    public void Default_IsIdentity()
    {
        var table = KeyExpansionTable.Default;
        ExpandedKey a = table.ExpandKey((byte)'a');
        Assert.True(a.IsHit((byte)'a'));
        Assert.False(a.IsHit((byte)'b'));
        Assert.False(a.IsHit((byte)'A'));
    }

    [Fact]
    public void Add_ExpandsKeyToMultipleValues()
    {
        var table = new KeyExpansionTable();
        table.Add((byte)'a', "abc"u8);

        ExpandedKey a = table.ExpandKey((byte)'a');
        Assert.True(a.IsHit((byte)'a')); // 恒等は残る
        Assert.True(a.IsHit((byte)'b'));
        Assert.True(a.IsHit((byte)'c'));
        Assert.False(a.IsHit((byte)'d'));

        // 他のキーは影響を受けない(恒等のまま)。
        ExpandedKey b = table.ExpandKey((byte)'b');
        Assert.True(b.IsHit((byte)'b'));
        Assert.False(b.IsHit((byte)'a'));
    }

    [Fact]
    public void HandlesHighByteValues()
    {
        var table = new KeyExpansionTable();
        table.Add(200, new byte[] { 255, 128 });
        ExpandedKey k = table.ExpandKey(200);
        Assert.True(k.IsHit(200));
        Assert.True(k.IsHit(255));
        Assert.True(k.IsHit(128));
        Assert.False(k.IsHit(0));
    }
}
