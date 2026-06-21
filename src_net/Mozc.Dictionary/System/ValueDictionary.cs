using Mozc.Base;
using Mozc.Storage.Louds;

namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/value_dictionary.{h,cc} 相当。
// 値 trie(エンコード済み値を格納)に対する suggestion-only 語の検索。
// IsValidKey: 先頭が ひらがな/カタカナ/漢字 でない(=英数記号等)もののみ対象。
// FillToken: key=value=対象文字列, cost=10000, lid=rid=suggestOnlyWordId。
public sealed class ValueDictionary : DictionaryBase
{
    private readonly LoudsTrie _valueTrie;
    private readonly SystemDictionaryCodec _codec = new();
    private readonly ushort _suggestionOnlyWordId;

    public ValueDictionary(ushort suggestionOnlyWordId, LoudsTrie valueTrie)
    {
        _valueTrie = valueTrie;
        _suggestionOnlyWordId = suggestionOnlyWordId;
    }

    private static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        ScriptType type = ScriptClassifier.GetFirstScriptType(key);
        return type is not (ScriptType.Hiragana or ScriptType.Kanji or ScriptType.Katakana);
    }

    private void FillToken(string value, Token token)
    {
        token.Key = value;
        token.Value = value;
        token.Cost = 10000;
        token.Lid = _suggestionOnlyWordId;
        token.Rid = _suggestionOnlyWordId;
        token.Attributes = Token.Attribute.None;
    }

    public override void LookupExact(string key, DictionaryCallback callback)
    {
        if (!IsValidKey(key))
        {
            return;
        }
        if (_valueTrie.ExactSearch(_codec.EncodeValue(key)) == -1)
        {
            return;
        }
        if (callback.OnKey(key) != DictionaryCallback.ResultType.TraverseContinue)
        {
            return;
        }
        if (callback.OnActualKey(key, key, 0) != DictionaryCallback.ResultType.TraverseContinue)
        {
            return;
        }
        var token = new Token();
        FillToken(key, token);
        callback.OnToken(key, key, token);
    }

    public override void LookupPredictive(string key, DictionaryCallback callback)
    {
        if (!IsValidKey(key))
        {
            return;
        }
        byte[] encodedPrefix = _codec.EncodeValue(key);
        foreach (var (encoded, _) in _valueTrie.PredictiveSearch(encodedPrefix))
        {
            string value = _codec.DecodeValue(encoded);
            var token = new Token();

            DictionaryCallback.ResultType r = callback.OnKey(value);
            if (r == DictionaryCallback.ResultType.TraverseDone) return;
            if (r == DictionaryCallback.ResultType.TraverseCull) continue;

            r = callback.OnActualKey(value, value, 0);
            if (r == DictionaryCallback.ResultType.TraverseDone) return;
            if (r == DictionaryCallback.ResultType.TraverseCull) continue;

            FillToken(value, token);
            if (callback.OnToken(value, value, token) == DictionaryCallback.ResultType.TraverseDone)
            {
                return;
            }
        }
    }
}
