using Mozc.Dictionary;
using Xunit;

namespace Mozc.Dictionary.Tests;

public class DictionaryInterfaceTests
{
    [Fact]
    public void Callback_Defaults_ToContinue()
    {
        var cb = new InlineDictionaryCallback();
        Assert.Equal(DictionaryCallback.ResultType.TraverseContinue, cb.OnKey("k"));
        Assert.Equal(DictionaryCallback.ResultType.TraverseContinue, cb.OnActualKey("k", "k", 0));
        Assert.Equal(DictionaryCallback.ResultType.TraverseContinue, cb.OnToken("k", "k", new Token()));
        Assert.False(cb.IsKanaModifierInsensitiveConversion);
    }

    [Fact]
    public void InlineCallback_DispatchesToHandlers()
    {
        string? seenKey = null;
        Token? seenToken = null;
        var cb = new InlineDictionaryCallback
        {
            KeyHandler = k => { seenKey = k; return DictionaryCallback.ResultType.TraverseNextKey; },
            TokenHandler = (k, ek, t) => { seenToken = t; return DictionaryCallback.ResultType.TraverseDone; },
            KanaModifierInsensitive = true,
        };

        Assert.Equal(DictionaryCallback.ResultType.TraverseNextKey, cb.OnKey("よみ"));
        Assert.Equal("よみ", seenKey);

        var token = new Token("よ", "読", 1, 2, 3);
        Assert.Equal(DictionaryCallback.ResultType.TraverseDone, cb.OnToken("よ", "よ", token));
        Assert.Same(token, seenToken);
        Assert.True(cb.IsKanaModifierInsensitiveConversion);
    }

    [Fact]
    public void DictionaryBase_DefaultsAreNoop()
    {
        var dict = new EmptyDictionary();
        Assert.False(dict.HasKey("x"));
        Assert.False(dict.HasValue("x"));
        Assert.False(dict.LookupComment("k", "v", out string comment));
        Assert.Equal(string.Empty, comment);
        dict.LookupExact("k", new InlineDictionaryCallback()); // 例外なく no-op
    }

    private sealed class EmptyDictionary : DictionaryBase;
}
