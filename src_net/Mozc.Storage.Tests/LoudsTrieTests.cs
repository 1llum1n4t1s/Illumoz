using System.Text;
using Mozc.Storage.Louds;
using Xunit;

namespace Mozc.Storage.Tests;

public class LoudsTrieTests
{
    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Build_Open_RoundTrips_Lookup_Restore_Prefix()
    {
        string[] keys =
        {
            "あ", "あい", "あいう", "か", "かき", "きく",
            "a", "ab", "abc", "tokyo", "東京", "東京都",
        };

        var builder = new LoudsTrieBuilder();
        foreach (string k in keys)
        {
            builder.Add(k);
        }
        byte[] image = builder.Build();

        var trie = new LoudsTrie();
        Assert.True(trie.Open(image));

        // 全キーが見つかり、ExactSearch == builder.GetId、復元も一致。
        foreach (string k in keys)
        {
            Assert.True(trie.HasKey(U(k)), $"HasKey({k})");
            int id = trie.ExactSearch(U(k));
            Assert.True(id >= 0);
            Assert.Equal(builder.GetId(k), id);
            Assert.Equal(U(k), trie.RestoreKeyBytes(id));
        }

        // 非キーは見つからない。
        foreach (string n in new[] { "", "い", "あいうえ", "ab" + "x", "東", "とう" })
        {
            if (n.Length == 0) continue;
            if (Array.IndexOf(keys, n) >= 0) continue;
            Assert.False(trie.HasKey(U(n)), $"!HasKey({n})");
            Assert.Equal(-1, trie.ExactSearch(U(n)));
        }

        // 前方一致: "あいう" の前置キー "あ", "あい", "あいう" が終端として返る。
        var prefixes = trie.PrefixSearch(U("あいう")).ToList();
        var restored = prefixes.Select(p => Encoding.UTF8.GetString(trie.RestoreKeyBytes(p.KeyId))).ToHashSet();
        Assert.Contains("あ", restored);
        Assert.Contains("あい", restored);
        Assert.Contains("あいう", restored);
    }

    [Fact]
    public void RandomKeySet_RoundTrips()
    {
        var rng = new Random(20260618);
        var keySet = new HashSet<string>();
        while (keySet.Count < 300)
        {
            int len = rng.Next(1, 8);
            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                // ひらがな + ASCII 英小文字を混在
                sb.Append(rng.Next(2) == 0
                    ? (char)('a' + rng.Next(26))
                    : (char)('ぁ' + rng.Next(80)));
            }
            keySet.Add(sb.ToString());
        }

        var builder = new LoudsTrieBuilder();
        foreach (string k in keySet)
        {
            builder.Add(k);
        }
        byte[] image = builder.Build();

        var trie = new LoudsTrie();
        Assert.True(trie.Open(image));

        foreach (string k in keySet)
        {
            int id = trie.ExactSearch(U(k));
            Assert.True(id >= 0, $"missing {k}");
            Assert.Equal(builder.GetId(k), id);
            Assert.Equal(k, Encoding.UTF8.GetString(trie.RestoreKeyBytes(id)));
        }

        // key id は 0..count-1 の一意な集合。
        var ids = keySet.Select(k => trie.ExactSearch(U(k))).ToHashSet();
        Assert.Equal(keySet.Count, ids.Count);
        Assert.Equal(0, ids.Min());
        Assert.Equal(keySet.Count - 1, ids.Max());

        // 確実に非キーなものは見つからない。
        Assert.Equal(-1, trie.ExactSearch(U("")));
    }
}
