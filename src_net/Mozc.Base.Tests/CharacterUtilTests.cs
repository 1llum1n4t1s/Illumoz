using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class CharacterUtilTests
{
    [Theory]
    [InlineData(0x3042)]  // あ
    [InlineData(0x41)]    // A
    [InlineData(0x1F600)] // 😀
    [InlineData(0x20)]    // 空白(>= 0x20 なので可)
    public void Acceptable(int cp)
    {
        Assert.True(CharacterUtil.IsAcceptableCharacterAsCandidate(cp));
    }

    [Theory]
    [InlineData(0x00)]      // NUL
    [InlineData(0x1F)]      // C0 制御
    [InlineData(0x7F)]      // DEL
    [InlineData(0x9F)]      // C1 末尾
    [InlineData(0x200E)]    // LRM(双方向制御)
    [InlineData(0x202A)]    // LRE
    [InlineData(0x2069)]    // PDI
    [InlineData(0x061C)]    // ALM
    [InlineData(0x110000)]  // 範囲外
    public void NotAcceptable(int cp)
    {
        Assert.False(CharacterUtil.IsAcceptableCharacterAsCandidate(cp));
    }
}
