using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Mozc.Storage.Louds;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class ValueDictionaryTests
{
    private const ushort SuggestId = 1234;
    private readonly SystemDictionaryCodec _codec = new();

    // 値集合(英数記号 = suggestion-only 対象)からエンコードして値 trie を構築。
    private LoudsTrie BuildValueTrie(params string[] values)
    {
        var builder = new LoudsTrieBuilder();
        foreach (string v in values)
        {
            builder.Add(_codec.EncodeValue(v));
        }
        var trie = new LoudsTrie();
        trie.Open(builder.Build());
        return trie;
    }

    private static (List<string> keys, List<Token> tokens) Collect()
    {
        return (new List<string>(), new List<Token>());
    }

    [Fact]
    public void LookupExact_FindsValidAsciiWord()
    {
        var trie = BuildValueTrie("google", "iphone", "fps");
        var dict = new ValueDictionary(SuggestId, trie);

        var (keys, tokens) = Collect();
        var cb = new InlineDictionaryCallback
        {
            TokenHandler = (k, ek, t) =>
            {
                keys.Add(k);
                tokens.Add(new Token(t.Key, t.Value, t.Cost, t.Lid, t.Rid, t.Attributes));
                return DictionaryCallback.ResultType.TraverseContinue;
            },
        };

        dict.LookupExact("google", cb);

        Assert.Single(tokens);
        Assert.Equal("google", tokens[0].Key);
        Assert.Equal("google", tokens[0].Value);
        Assert.Equal(10000, tokens[0].Cost);
        Assert.Equal(SuggestId, tokens[0].Lid);
        Assert.Equal(SuggestId, tokens[0].Rid);
    }

    [Fact]
    public void LookupExact_MissingWord_NoCallback()
    {
        var trie = BuildValueTrie("google");
        var dict = new ValueDictionary(SuggestId, trie);
        int count = 0;
        var cb = new InlineDictionaryCallback
        {
            TokenHandler = (_, _, _) => { count++; return DictionaryCallback.ResultType.TraverseContinue; },
        };
        dict.LookupExact("apple", cb);
        Assert.Equal(0, count);
    }

    [Fact]
    public void LookupExact_HiraganaKey_Rejected()
    {
        var trie = BuildValueTrie("google");
        var dict = new ValueDictionary(SuggestId, trie);
        int count = 0;
        var cb = new InlineDictionaryCallback
        {
            TokenHandler = (_, _, _) => { count++; return DictionaryCallback.ResultType.TraverseContinue; },
        };
        dict.LookupExact("ひらがな", cb); // 先頭がひらがな → IsValidKey false
        Assert.Equal(0, count);
    }

    [Fact]
    public void LookupPredictive_ReturnsAllWithPrefix()
    {
        var trie = BuildValueTrie("google", "good", "goal", "iphone");
        var dict = new ValueDictionary(SuggestId, trie);

        var (keys, _) = Collect();
        var cb = new InlineDictionaryCallback
        {
            TokenHandler = (k, _, _) => { keys.Add(k); return DictionaryCallback.ResultType.TraverseContinue; },
        };

        dict.LookupPredictive("go", cb);

        keys.Sort();
        Assert.Equal(new[] { "goal", "good", "google" }, keys);
    }

    [Fact]
    public void LookupPredictive_DoneStopsTraversal()
    {
        var trie = BuildValueTrie("a1", "a2", "a3", "a4");
        var dict = new ValueDictionary(SuggestId, trie);
        int count = 0;
        var cb = new InlineDictionaryCallback
        {
            TokenHandler = (_, _, _) => { count++; return DictionaryCallback.ResultType.TraverseDone; },
        };
        dict.LookupPredictive("a", cb);
        Assert.Equal(1, count); // 最初の1件で停止
    }
}
