using System.Text;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class SystemDictionaryCodecKeyTests
{
    private readonly SystemDictionaryCodec _codec = new();

    [Theory]
    [InlineData("あいうえお")]
    [InlineData("がぎぐげご")]
    [InlineData("アイウエオ")]
    [InlineData("・ー")]            // 0x30FB, 0x30FC
    [InlineData("とうきょうきょうと")]
    [InlineData("")]
    public void EncodeKey_IsSelfInverse(string key)
    {
        string encoded = _codec.EncodeKey(key);
        Assert.Equal(key, _codec.DecodeKey(encoded));   // encode→decode で復元
        Assert.Equal(key, _codec.EncodeKey(encoded));   // 対称変換(自己反転)
    }

    [Fact]
    public void Hiragana_EncodesToSingleByteRange()
    {
        // "あ"(U+3042) → 0x3042-0x3040 = 0x0002 (UTF-8 で 1 バイト)
        string encoded = _codec.EncodeKey("あ");
        Assert.Equal(1, encoded.Length);
        Assert.Equal(0x0002, encoded[0]);
        Assert.Equal(1, _codec.GetEncodedKeyLength("あ"));

        // ひらがな列はキー長 = 文字数(各 1 バイト)
        Assert.Equal(5, _codec.GetEncodedKeyLength("あいうえお"));
    }

    [Fact]
    public void RandomHiraganaKatakana_RoundTrips()
    {
        var rng = new Random(99);
        for (int t = 0; t < 200; t++)
        {
            var sb = new StringBuilder();
            int len = rng.Next(0, 10);
            for (int i = 0; i < len; i++)
            {
                sb.Append((char)(rng.Next(2) == 0
                    ? rng.Next(0x3041, 0x3096)
                    : rng.Next(0x30a1, 0x30fd)));
            }
            string key = sb.ToString();
            Assert.Equal(key, _codec.DecodeKey(_codec.EncodeKey(key)));
        }
    }
}
