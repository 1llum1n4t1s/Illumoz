using Mozc.Dictionary;
using Xunit;
using E = Mozc.Dictionary.UserDictionaryStorage.UserEntry;

namespace Mozc.Dictionary.Tests;

public class UserDictionaryStorageTests
{
    [Fact]
    public void AddLookup_PrefixAndExact()
    {
        var s = new UserDictionaryStorage();
        Assert.True(s.Add(new E("もずく", "Mozc", "名詞", "")));
        Assert.True(s.Add(new E("もずくる", "MozcRu", "名詞", "")));

        Assert.Equal(2, s.LookupPredictive("もずく").Count);
        Assert.Single(s.LookupExact("もずく"));
        Assert.Equal("Mozc", s.LookupExact("もずく")[0].Word);
    }

    [Fact]
    public void Add_RejectsDuplicateAndEmpty()
    {
        var s = new UserDictionaryStorage();
        Assert.True(s.Add(new E("あ", "亜", "名詞", "")));
        Assert.False(s.Add(new E("あ", "亜", "名詞", ""))); // 重複
        Assert.False(s.Add(new E("", "x", "名詞", "")));    // 空 reading
        Assert.Equal(1, s.Count);
    }

    [Fact]
    public void Remove_Works()
    {
        var s = new UserDictionaryStorage();
        s.Add(new E("あ", "亜", "名詞", ""));
        Assert.True(s.Remove("あ", "亜"));
        Assert.Equal(0, s.Count);
        Assert.False(s.Remove("あ", "亜"));
    }

    [Fact]
    public void Serialize_Load_RoundTrip()
    {
        var s = new UserDictionaryStorage();
        s.Add(new E("もずく", "Mozc", "名詞", "メモ"));
        s.Add(new E("わたし", "渡し", "名詞", ""));
        byte[] bytes = s.Serialize();

        var s2 = new UserDictionaryStorage();
        Assert.True(s2.Load(bytes));
        Assert.Equal(2, s2.Count);
        Assert.Equal("Mozc", s2.LookupExact("もずく")[0].Word);
        Assert.Equal("メモ", s2.LookupExact("もずく")[0].Comment);
    }

    [Fact]
    public void Deterministic_SameContentSameBytes()
    {
        var a = new UserDictionaryStorage();
        a.Add(new E("b", "B", "名詞", ""));
        a.Add(new E("a", "A", "名詞", ""));
        var b = new UserDictionaryStorage();
        b.Add(new E("a", "A", "名詞", ""));
        b.Add(new E("b", "B", "名詞", ""));
        Assert.Equal(a.Serialize(), b.Serialize());
    }

    [Fact]
    public void Load_RejectsBadMagic()
        => Assert.False(new UserDictionaryStorage().Load(new byte[] { 9, 9, 9, 9, 0, 0, 0, 0 }));
}
