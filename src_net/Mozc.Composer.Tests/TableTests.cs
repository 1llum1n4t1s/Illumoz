using Mozc.Composer;
using Xunit;

namespace Mozc.Composer.Tests;

public class TableTests
{
    private static Table SmallRoman()
    {
        var t = new Table();
        // ka->か, ki->き, ku->く, kk->っ(pending k), n->ん, na->な
        t.LoadFromString("ka\tか\nki\tき\nku\tく\nkk\tっ\tk\nn\tん\nna\tな");
        return t;
    }

    [Fact]
    public void LoadFromString_ParsesTwoAndThreeColumns()
    {
        Table t = SmallRoman();
        Entry? ka = t.LookUp("ka");
        Assert.NotNull(ka);
        Assert.Equal("か", ka!.Result);
        Assert.Equal("", ka.Pending);

        Entry? kk = t.LookUp("kk");
        Assert.NotNull(kk);
        Assert.Equal("っ", kk!.Result);
        Assert.Equal("k", kk.Pending);
    }

    [Fact]
    public void LookUpPrefix_FixedAndAmbiguous()
    {
        Table t = SmallRoman();

        // "ka" は確定一致(より長い規則なし)→ fixed=true
        Entry? e1 = t.LookUpPrefix("ka", out int len1, out bool fixed1);
        Assert.NotNull(e1);
        Assert.Equal(2, len1);
        Assert.True(fixed1);

        // "n" は "n"(ん) と "na"(な) があり曖昧 → データはあるが fixed=false
        Entry? e2 = t.LookUpPrefix("n", out int len2, out bool fixed2);
        Assert.NotNull(e2);
        Assert.Equal("ん", e2!.Result);
        Assert.Equal(1, len2);
        Assert.False(fixed2);

        // "ke" は "k" 配下にあるが "ke" のデータ無し → null, keyLength=1
        Entry? e3 = t.LookUpPrefix("ke", out int len3, out _);
        Assert.Null(e3);
        Assert.Equal(1, len3);
    }

    [Fact]
    public void HasSubRules_DetectsPrefixes()
    {
        Table t = SmallRoman();
        Assert.True(t.HasSubRules("k"));   // ka/ki/ku/kk
        Assert.False(t.HasSubRules("z"));  // 規則なし
    }

    [Fact]
    public void CaseSensitive_NormalizesLowerByDefault()
    {
        Table t = SmallRoman();
        Assert.False(t.CaseSensitive);
        Assert.NotNull(t.LookUp("KA")); // 小文字化されて ka にヒット
    }
}
