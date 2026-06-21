using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Dictionary.Tests;

// SystemDictionaryBuilder で組んだ辞書を SystemDictionary で読み戻す往復検証。
// builder/reader が同一 codec・同一かな変換を使うため、トークン内容が厳密に復元される。
public class SystemDictionaryTests
{
    private static Token T(string key, string value, int cost, ushort lid, ushort rid,
        Token.Attribute attr = Token.Attribute.None)
        => new(key, value, cost, lid, rid, attr);

    private static List<Token> CollectExact(SystemDictionary dict, string key)
    {
        var tokens = new List<Token>();
        dict.LookupExact(key, new InlineDictionaryCallback
        {
            TokenHandler = (_, _, token) =>
            {
                tokens.Add(token);
                return DictionaryCallback.ResultType.TraverseContinue;
            },
        });
        return tokens;
    }

    private static SystemDictionary BuildDict(IEnumerable<Token> tokens)
        => new SystemDictionaryBuilder().Build(tokens);

    [Fact]
    public void LookupExact_SingleToken_RoundTrips()
    {
        var dict = BuildDict(new[] { T("とうきょう", "東京", 1234, 100, 200) });
        List<Token> result = CollectExact(dict, "とうきょう");

        Assert.Single(result);
        Assert.Equal("とうきょう", result[0].Key);
        Assert.Equal("東京", result[0].Value);
        Assert.Equal(1234, result[0].Cost);
        Assert.Equal(100, result[0].Lid);
        Assert.Equal(200, result[0].Rid);
    }

    [Fact]
    public void LookupExact_MultipleTokens_SameKey()
    {
        var dict = BuildDict(new[]
        {
            T("あめ", "雨", 500, 10, 10),
            T("あめ", "飴", 600, 20, 20),
            T("あめ", "アメ", 700, 30, 30), // value == HiraganaToKatakana(key) => AS_IS_KATAKANA
        });
        List<Token> result = CollectExact(dict, "あめ");

        Assert.Equal(3, result.Count);
        var values = result.Select(t => (t.Value, t.Cost, t.Lid, t.Rid)).ToHashSet();
        Assert.Contains(("雨", 500, (ushort)10, (ushort)10), values);
        Assert.Contains(("飴", 600, (ushort)20, (ushort)20), values);
        Assert.Contains(("アメ", 700, (ushort)30, (ushort)30), values);
    }

    [Fact]
    public void LookupExact_AsIsHiragana_ValueEqualsKey()
    {
        var dict = BuildDict(new[] { T("もずく", "もずく", 800, 5, 5) });
        List<Token> result = CollectExact(dict, "もずく");
        Assert.Single(result);
        Assert.Equal("もずく", result[0].Value);
        Assert.Equal(800, result[0].Cost);
    }

    [Fact]
    public void LookupExact_NonexistentKey_ReturnsNothing()
    {
        var dict = BuildDict(new[] { T("とうきょう", "東京", 1234, 100, 200) });
        Assert.Empty(CollectExact(dict, "おおさか"));
    }

    [Fact]
    public void HasKey_HasValue()
    {
        var dict = BuildDict(new[]
        {
            T("とうきょう", "東京", 1234, 100, 200),
            T("あめ", "雨", 500, 10, 10),
        });
        Assert.True(dict.HasKey("とうきょう"));
        Assert.True(dict.HasKey("あめ"));
        Assert.False(dict.HasKey("きょうと"));
        Assert.True(dict.HasValue("東京"));
        Assert.True(dict.HasValue("雨"));
        Assert.False(dict.HasValue("大阪"));
    }

    [Fact]
    public void LookupPrefix_FindsAllPrefixes()
    {
        var dict = BuildDict(new[]
        {
            T("き", "気", 100, 1, 1),
            T("きょう", "今日", 200, 2, 2),
            T("きょうと", "京都", 300, 3, 3),
        });

        var prefixes = new List<(string Key, string Value)>();
        dict.LookupPrefix("きょうと", new InlineDictionaryCallback
        {
            TokenHandler = (key, _, token) =>
            {
                prefixes.Add((key, token.Value));
                return DictionaryCallback.ResultType.TraverseContinue;
            },
        });

        Assert.Contains(("き", "気"), prefixes);
        Assert.Contains(("きょう", "今日"), prefixes);
        Assert.Contains(("きょうと", "京都"), prefixes);
    }

    [Fact]
    public void LookupPredictive_FindsAllWithPrefix()
    {
        var dict = BuildDict(new[]
        {
            T("きょう", "今日", 200, 2, 2),
            T("きょうと", "京都", 300, 3, 3),
            T("きょうし", "教師", 400, 4, 4),
            T("おおさか", "大阪", 500, 5, 5),
        });

        var keys = new HashSet<string>();
        dict.LookupPredictive("きょう", new InlineDictionaryCallback
        {
            TokenHandler = (key, _, _) =>
            {
                keys.Add(key);
                return DictionaryCallback.ResultType.TraverseContinue;
            },
        });

        Assert.Contains("きょう", keys);
        Assert.Contains("きょうと", keys);
        Assert.Contains("きょうし", keys);
        Assert.DoesNotContain("おおさか", keys);
    }

    [Fact]
    public void LookupExact_ManyEntries_AllRoundTrip()
    {
        // 多数エントリで frequent pos map / cost type / same-as-prev を実際に通す。
        var tokens = new List<Token>();
        for (int i = 0; i < 50; i++)
        {
            string key = "かな" + new string('あ', i % 5 + 1);
            tokens.Add(T(key, "漢" + i, 1000 + i, (ushort)(i % 7), (ushort)(i % 7)));
        }
        var dict = BuildDict(tokens);

        foreach (Token expected in tokens)
        {
            List<Token> got = CollectExact(dict, expected.Key);
            Assert.Contains(got, t =>
                t.Value == expected.Value && t.Cost == expected.Cost &&
                t.Lid == expected.Lid && t.Rid == expected.Rid);
        }
    }
}
