namespace Mozc.Dictionary;

// C++ src/dictionary/dictionary_interface.h の Callback 相当。
// trie 走査時のコールバック。戻り値で走査制御。
public abstract class DictionaryCallback
{
    public enum ResultType
    {
        TraverseDone,     // 走査終了
        TraverseNextKey,  // 次のキーへ
        TraverseCull,     // この部分木を枝刈り
        TraverseContinue, // 続行
    }

    public virtual ResultType OnKey(string key) => ResultType.TraverseContinue;

    public virtual ResultType OnActualKey(string key, string actualKey, int numExpanded)
        => ResultType.TraverseContinue;

    public virtual ResultType OnToken(string key, string expandedKey, Token token)
        => ResultType.TraverseContinue;

    public virtual bool IsKanaModifierInsensitiveConversion => false;
}

// 各辞書実装の基底。C++ DictionaryInterface 相当(既定は no-op)。
public abstract class DictionaryBase
{
    public virtual bool HasKey(string key) => false;
    public virtual bool HasValue(string value) => false;
    public virtual void LookupPredictive(string key, DictionaryCallback callback) { }
    public virtual void LookupPrefix(string key, DictionaryCallback callback) { }
    public virtual void LookupExact(string key, DictionaryCallback callback) { }
    public virtual void LookupReverse(string str, DictionaryCallback callback) { }

    public virtual bool LookupComment(string key, string value, out string comment)
    {
        comment = string.Empty;
        return false;
    }

    public virtual void PopulateReverseLookupCache(string str) { }
    public virtual void ClearReverseLookupCache() { }
}

// C++ InlineCallback 相当。デリゲートでコールバックを差し込む。
public sealed class InlineDictionaryCallback : DictionaryCallback
{
    public Func<string, ResultType>? KeyHandler { get; init; }
    public Func<string, string, int, ResultType>? ActualKeyHandler { get; init; }
    public Func<string, string, Token, ResultType>? TokenHandler { get; init; }
    public bool KanaModifierInsensitive { get; init; }

    public override ResultType OnKey(string key)
        => KeyHandler?.Invoke(key) ?? ResultType.TraverseContinue;

    public override ResultType OnActualKey(string key, string actualKey, int numExpanded)
        => ActualKeyHandler?.Invoke(key, actualKey, numExpanded) ?? ResultType.TraverseContinue;

    public override ResultType OnToken(string key, string expandedKey, Token token)
        => TokenHandler?.Invoke(key, expandedKey, token) ?? ResultType.TraverseContinue;

    public override bool IsKanaModifierInsensitiveConversion => KanaModifierInsensitive;
}
