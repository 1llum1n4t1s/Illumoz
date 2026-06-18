using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class SmallLetterRewriterTests
{
    private static Segments OneSegment(string key)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        Candidate c = seg.AddCandidate();
        c.Key = key;
        c.Value = key;
        c.ContentKey = key;
        c.ContentValue = key;
        return segments;
    }

    [Theory]
    [InlineData("x^2", "x²")]
    [InlineData("a_1", "a₁")]
    [InlineData("^123", "¹²³")]           // 連続数字は SuperDigit 継続
    [InlineData("_12", "₁₂")]
    [InlineData("x^2_3", "x²₃")]          // 上付き→下付き切替
    [InlineData("^+", "⁺")]              // 記号
    [InlineData("CO_2", "CO₂")]
    public void Convert_Valid(string key, string expected)
    {
        Assert.True(SmallLetterRewriter.TryConvert(key, out string value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc")]      // ^/_ なし
    [InlineData("^")]        // 末尾 ^ のみ(変換なし→元に戻る)
    [InlineData("_")]        // 末尾 _ のみ
    [InlineData("^z")]       // テーブル外文字は ^z のまま=変換なし
    public void Convert_NoChange(string key)
    {
        Assert.False(SmallLetterRewriter.TryConvert(key, out _));
    }

    [Fact]
    public void Rewrite_InsertsCandidate()
    {
        var segments = OneSegment("x^2");
        var rw = new SmallLetterRewriter();
        Assert.True(rw.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        bool found = false;
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            if (seg.Get(i).Value == "x²")
            {
                found = true;
                Assert.Equal("上下付き文字", seg.Get(i).Description);
            }
        }
        Assert.True(found);
    }
}
