using System.Text;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class SystemDictionaryCodecTests
{
    private readonly SystemDictionaryCodec _codec = new();

    [Theory]
    [InlineData("あいうえお")]          // ひらがな
    [InlineData("アイウエオ")]          // カタカナ
    [InlineData("東京都")]              // 漢字(0x4e00-0x97ff)
    [InlineData("abcABC123")]           // ASCII
    [InlineData("、。！？")]            // 記号
    [InlineData("𠮷野家")]              // サロゲート(supplementary plane)
    [InlineData("Ｆｕｌｌｗｉｄｔｈ")]   // 全角英字
    [InlineData("錆")]                  // 漢字(low byte 0 でない)
    [InlineData("一")]                  // 0x4e00(low byte 0 → XX00 経路)
    [InlineData("あA東𠮷、")]           // 混在
    [InlineData("")]                    // 空
    public void EncodeValue_DecodeValue_RoundTrips(string value)
    {
        byte[] encoded = _codec.EncodeValue(value);
        string decoded = _codec.DecodeValue(encoded);
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void RandomUnicode_RoundTrips()
    {
        var rng = new Random(20260618);
        for (int t = 0; t < 200; t++)
        {
            int len = rng.Next(0, 12);
            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                int cp;
                do
                {
                    cp = rng.Next(0x20, 0x30000);
                }
                while (cp is >= 0xD800 and <= 0xDFFF); // サロゲート単体を除外
                sb.Append(char.ConvertFromUtf32(cp));
            }
            string s = sb.ToString();
            Assert.Equal(s, _codec.DecodeValue(_codec.EncodeValue(s)));
        }
    }
}
