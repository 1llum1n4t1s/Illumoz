using System.Text;
using Mozc.Storage.Louds;
using Xunit;

namespace Mozc.Storage.Tests;

public class LoudsTriePredictiveTests
{
    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    private static LoudsTrie Build(params string[] keys)
    {
        var b = new LoudsTrieBuilder();
        foreach (string k in keys) b.Add(k);
        var trie = new LoudsTrie();
        trie.Open(b.Build());
        return trie;
    }

    [Fact]
    public void PredictiveSearch_ReturnsAllKeysWithPrefix()
    {
        var trie = Build("あ", "あい", "あいう", "あお", "か", "かき");

        var got = trie.PredictiveSearch(U("あ"))
            .Select(r => Encoding.UTF8.GetString(r.Key))
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(new[] { "あ", "あい", "あいう", "あお" }, got);
    }

    [Fact]
    public void PredictiveSearch_KeyIdsMatchExactSearch()
    {
        var trie = Build("あ", "あい", "あいう", "あお", "か");
        foreach (var (key, keyId) in trie.PredictiveSearch(U("あ")))
        {
            Assert.Equal(trie.ExactSearch(key), keyId);
        }
    }

    [Fact]
    public void PredictiveSearch_NonexistentPrefix_Empty()
    {
        var trie = Build("あ", "か");
        Assert.Empty(trie.PredictiveSearch(U("さ")));
    }

    [Fact]
    public void PredictiveSearch_DeepPrefix_OnlyMatching()
    {
        var trie = Build("とうきょう", "とうきょうと", "とうほく", "おおさか");
        var got = trie.PredictiveSearch(U("とうきょう"))
            .Select(r => Encoding.UTF8.GetString(r.Key))
            .OrderBy(s => s).ToList();
        Assert.Equal(new[] { "とうきょう", "とうきょうと" }, got);
    }
}
