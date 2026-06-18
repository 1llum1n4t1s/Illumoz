using System.Text;
using Mozc.Base;

namespace Mozc.Composer;

// C++ composer::CharChunk 相当。raw(生入力)/conversion(確定変換)/pending(未確定残り)/
// ambiguous(曖昧な確定候補, 例:単独 "n"→"ん")を保持し、AddInput で trie を引いて遷移する。
// transliterator は現状 LOCAL=conversion をそのまま返す簡略実装(半角/全角等は後続)。
public sealed class CharChunk
{
    private readonly Table _table;
    private string _raw = string.Empty;
    private string _conversion = string.Empty;
    private string _pending = string.Empty;
    private string _ambiguous = string.Empty;
    private TableAttributes _attributes = TableAttributes.NoTableAttribute;

    public CharChunk(Table table)
    {
        _table = table;
    }

    public string Raw => _raw;
    public string Conversion => _conversion;
    public string Pending => _pending;
    public TableAttributes Attributes => _attributes;

    public bool IsEmpty => _raw.Length == 0 && _conversion.Length == 0 && _pending.Length == 0;

    // transliterate: 現状 LOCAL=converted をそのまま返す(raw は半角 ASCII 変換等で使う想定)。
    private static string Transliterate(string raw, string converted) => converted;

    // 特殊キー除去: 現状 romanji-hiragana.tsv は特殊キーを含まないため恒等。
    private static string DeleteSpecialKeys(string s) => s;

    public int GetLength()
        => ScriptClassifier.CharsLen(Transliterate(DeleteSpecialKeys(_raw),
            DeleteSpecialKeys(_conversion + _pending)));

    public void AppendResult(StringBuilder result)
        => result.Append(Transliterate(DeleteSpecialKeys(_raw),
            DeleteSpecialKeys(_conversion + _pending)));

    // 確定値のみ(conversion + 確定可能な pending)を追加。
    public void AppendTrimedResult(StringBuilder result)
    {
        string converted = _conversion;
        if (_pending.Length != 0)
        {
            Entry? entry = _table.LookUpPrefix(_pending, out _, out _);
            if (entry != null && entry.Input == entry.Result)
            {
                converted += entry.Result;
            }
        }
        result.Append(Transliterate(DeleteSpecialKeys(_raw), DeleteSpecialKeys(converted)));
    }

    // 未確定も含め確定扱いで追加(ambiguous を優先)。
    public void AppendFixedResult(StringBuilder result)
    {
        string converted = _ambiguous.Length != 0
            ? _conversion + _ambiguous
            : _conversion + _pending;
        result.Append(Transliterate(DeleteSpecialKeys(_raw), DeleteSpecialKeys(converted)));
    }

    public bool IsFixed => _pending.Length == 0;

    public bool IsAppendable(Table table) => _pending.Length != 0 && ReferenceEquals(table, _table);

    public bool IsConvertible(Table table, string input)
    {
        if (!IsAppendable(table))
        {
            return false;
        }
        string key = _pending + input;
        Entry? entry = table.LookUpPrefix(key, out int keyLength, out bool isFixed);
        return entry != null && key.Length == keyLength && isFixed;
    }

    public void Combine(CharChunk leftChunk)
    {
        _conversion = leftChunk._conversion + _conversion;
        _raw = leftChunk._raw + _raw;
        if (leftChunk._ambiguous.Length == 0)
        {
            _ambiguous = string.Empty;
        }
        else if (_ambiguous.Length == 0)
        {
            _ambiguous = leftChunk._ambiguous + _pending;
        }
        else
        {
            _ambiguous = leftChunk._ambiguous + _ambiguous;
        }
        _pending = leftChunk._pending + _pending;
    }

    public bool ShouldCommit => _attributes.HasFlag(TableAttributes.DirectInput) && _pending.Length == 0;

    public bool ShouldInsertNewChunk(CompositionInput input)
    {
        if (_raw.Length == 0 && _conversion.Length == 0 && _pending.Length == 0)
        {
            return false;
        }
        bool isNewInput = input.IsNewInput
            || (_attributes.HasFlag(TableAttributes.EndChunk) && _pending.Length == 0);
        if (isNewInput && (_table.HasNewChunkEntry(input.Raw) || !_table.HasSubRules(input.Raw)))
        {
            return true;
        }
        return false;
    }

    public void AddCompositionInput(CompositionInput input)
    {
        if (input.Conversion.Length != 0)
        {
            AddInputAndConvertedChar(input);
            return;
        }
        if (ShouldInsertNewChunk(input))
        {
            return;
        }
        string raw = input.Raw;
        AddInput(ref raw);
        input.Raw = raw;
    }

    public void AddInput(ref string input)
    {
        string tmp = input;
        bool loop = true;
        while (loop)
        {
            (loop, tmp) = AddInputInternal(tmp);
        }
        input = tmp;
    }

    // C++ CharChunk::AddInputInternal の忠実移植。戻り値 (継続するか, 未消費 input)。
    private (bool Loop, string Remaining) AddInputInternal(string input)
    {
        const bool kLoop = true;
        const bool kNoLoop = false;

        string key = _pending + input;
        Entry? entry = _table.LookUpPrefix(key, out int usedKeyLength, out bool isFixed);

        if (entry == null)
        {
            if (usedKeyLength == 0)
            {
                // 特殊キーのトリム(現状恒等なので発火しない)。
                if (_pending.Length == 0)
                {
                    FrontChar(input, out string front, out string rest);
                    _raw += front;
                    _conversion += front;
                    input = rest;
                }
                return (kNoLoop, input);
            }
            if (usedKeyLength == _pending.Length)
            {
                return (kNoLoop, input);
            }
            if (usedKeyLength < _pending.Length)
            {
                return (kNoLoop, input);
            }
            // table 内に前方一致はあるが結果未到達(pending のみ)。input を pending へ移す。
            int usedInputLen = usedKeyLength - _pending.Length;
            string usedInputChars = input.Substring(0, usedInputLen);
            _raw += usedInputChars;
            _pending += usedInputChars;
            _ambiguous = string.Empty;
            return (kNoLoop, input.Substring(usedInputLen));
        }

        // key の前方一致が変換結果に到達(entry != null)。
        bool isFirstEntry = _conversion.Length == 0
            && (_raw.Length == 0 || _pending.Length == 0 || _raw == _pending);
        if (isFirstEntry)
        {
            _attributes = entry.Attributes;
        }

        int usedInputLength = usedKeyLength - _pending.Length;
        _raw += input.Substring(0, usedInputLength);
        input = input.Substring(usedInputLength);

        if (isFixed || key.Length > usedKeyLength)
        {
            _conversion += entry.Result;
            _pending = entry.Pending;
            _ambiguous = string.Empty;
        }
        else
        {
            _pending = key;
            _ambiguous = entry.Result;
        }

        if (isFixed && input.Length == 0 && _conversion.Length == 0 && _pending.Length == 0
            && _attributes.HasFlag(TableAttributes.NoTransliteration))
        {
            _raw = string.Empty;
            return (kNoLoop, input);
        }

        if (input.Length == 0 || _pending.Length == 0)
        {
            return (kNoLoop, input);
        }
        return (kLoop, input);
    }

    private void AddInputAndConvertedChar(CompositionInput input)
    {
        if (input.IsAsis)
        {
            if (IsEmpty)
            {
                _raw = input.Raw;
                _conversion = input.Conversion;
                input.Clear();
            }
            return;
        }

        if (IsEmpty)
        {
            _raw = input.Raw;
            input.Raw = string.Empty;
            _pending = input.Conversion;
            _ambiguous = input.Conversion;
            input.Conversion = string.Empty;
            Entry? e = _table.LookUp(_pending);
            if (e != null)
            {
                _attributes = e.Attributes;
            }
            return;
        }

        string keyInput = _pending + input.Conversion;
        Entry? entry = _table.LookUpPrefix(keyInput, out int keyLength, out bool isFixed);
        if (entry == null)
        {
            return;
        }
        if (keyLength == keyInput.Length)
        {
            _raw += input.Raw;
            if (isFixed)
            {
                _conversion += entry.Result;
                _pending = entry.Pending;
                _ambiguous = string.Empty;
            }
            else
            {
                _pending = entry.Result;
                _ambiguous = entry.Result;
            }
            input.Raw = string.Empty;
            input.Conversion = string.Empty;
            return;
        }
        if (keyLength == _pending.Length)
        {
            return;
        }
        _raw += input.Raw;
        _conversion += entry.Result;
        _pending = entry.Pending;
        input.Raw = string.Empty;
        input.Conversion = keyInput.Substring(keyLength);
    }

    private static void FrontChar(string s, out string front, out string rest)
    {
        Rune.DecodeFromUtf16(s, out Rune rune, out int consumed);
        front = s.Substring(0, consumed);
        rest = s.Substring(consumed);
    }
}
