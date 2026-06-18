using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

// C++ base/hash_test.cc の既知ベクタでバイト互換を検証(ゴールデン)。
public class LegacyFingerprintTests
{
    private const string Long = "Hello, world!  Hello, Tokyo!  Good afternoon!  Ladies and gentlemen.";

    [Theory]
    [InlineData("", 0x0d46d8e3u)]
    [InlineData("google", 0x74290877u)]
    [InlineData(Long, 0xb0f5a2bau)]
    public void Fingerprint32_MatchesCxx(string s, uint expected)
        => Assert.Equal(expected, LegacyFingerprint.Fingerprint32(s));

    [Theory]
    [InlineData("", 0x2dcdbae1b24d9501ul)]
    [InlineData("google", 0x56d4ad5eafa6beedul)]
    [InlineData(Long, 0x936ccddf9d4f0b39ul)]
    public void Fingerprint64_MatchesCxx(string s, ulong expected)
        => Assert.Equal(expected, LegacyFingerprint.Fingerprint(s));

    [Theory]
    [InlineData("", 0x1153f4beb24d9501ul)]
    [InlineData("google", 0x1f8cbc0cafa6beedul)]
    [InlineData(Long, 0xe3fd29979d4f0b39ul)]
    public void FingerprintWithSeed_MatchesCxx(string s, ulong expected)
        => Assert.Equal(expected, LegacyFingerprint.FingerprintWithSeed(s, 0xdeadbeef));
}
