using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

public class NodeTests
{
    [Fact]
    public void InitFromToken_MapsFieldsAndAttributes()
    {
        var token = new Token("よみ", "読み", 123, 10, 20,
            Token.Attribute.UserDictionary | Token.Attribute.SpellingCorrection);
        var node = new Node();
        node.InitFromToken(token);

        Assert.Equal("よみ", node.Key);
        Assert.Equal("読み", node.Value);
        Assert.Equal(123, node.Wcost);
        Assert.Equal(0, node.Cost);
        Assert.Equal(10, node.Lid);
        Assert.Equal(20, node.Rid);
        Assert.Equal(Node.NodeType.NorNode, node.Type);
        Assert.True(node.Attributes.HasFlag(Node.Attribute.UserDictionary));
        Assert.True(node.Attributes.HasFlag(Node.Attribute.NoVariantsExpansion));
        Assert.True(node.Attributes.HasFlag(Node.Attribute.SpellingCorrection));
        Assert.False(node.Attributes.HasFlag(Node.Attribute.SuffixDictionary));
    }

    [Fact]
    public void Segmenter_NodeOverload_HandlesBosEosSingleParticle()
    {
        // l_table/r_table 恒等、IsBoundary(rid,lid) = (rid+lid) が偶数 とする 4x4。
        int n = 4;
        ushort[] lTable = { 0, 1, 2, 3 };
        ushort[] rTable = { 0, 1, 2, 3 };
        byte[] bitarray = new byte[(n * n + 7) / 8];
        for (int rid = 0; rid < n; rid++)
        {
            for (int lid = 0; lid < n; lid++)
            {
                if ((rid + lid) % 2 == 0)
                {
                    int idx = rid + n * lid;
                    bitarray[idx >> 3] |= (byte)(1 << (idx & 7));
                }
            }
        }
        var seg = new Segmenter(n, n, lTable, rTable, bitarray, new ushort[2 * n]);

        var bos = new Node { Type = Node.NodeType.BosNode };
        var eos = new Node { Type = Node.NodeType.EosNode };
        var a = new Node { Rid = 1, Lid = 1 };
        var b = new Node { Rid = 2, Lid = 2 };

        Assert.True(seg.IsBoundary(bos, a, false));   // BOS は常に境界
        Assert.True(seg.IsBoundary(a, eos, false));   // EOS は常に境界
        Assert.False(seg.IsBoundary(a, b, true));     // single segment は境界なし
        a.Attributes |= Node.Attribute.StartsWithParticle;
        Assert.False(seg.IsBoundary(a, b, false));    // 助詞始まりは境界なし
        a.Attributes = Node.Attribute.Default;
        // a.rid=1, b.lid=2 → 1+2=3 奇数 → bit 未設定 → false
        Assert.False(seg.IsBoundary(a, b, false));
        // a.rid=1, lid=1 → 1+1=2 偶数 → true
        Assert.True(seg.IsBoundary(a, a, false));
    }
}
