namespace Mozc.Composer;

// C++ composer::Composer 相当(中核フローのみ)。Table を使って打鍵列を保持し、
// preedit(表示)と変換用クエリ(確定)を返す。番号変換・全半角変換等の後処理は後続。
public sealed class Composer
{
    private readonly Composition _composition;

    public Composer(Table table)
    {
        _composition = new Composition(table);
    }

    public void Reset() => _composition.Erase();

    // 1 打鍵分の生入力を投入(ローマ字 1 文字等)。
    public void InsertCharacter(string key)
    {
        _composition.InsertInput(CompositionInput.FromRaw(key));
    }

    public void InsertCharacters(string keys)
    {
        foreach (char c in keys)
        {
            InsertCharacter(c.ToString());
        }
    }

    // preedit 表示用文字列(未確定 pending を含む)。
    public string GetStringForPreedit() => _composition.GetString();

    // 変換にかけるクエリ(pending を確定扱いにトリム)。
    public string GetQueryForConversion() => _composition.GetStringWithTrimMode(TrimMode.Fix);

    // 表記変換候補(F6=ひらがな/F7=全角カナ/F8=半角カナ/F9=全角英数/F10=半角英数)。
    public string GetStringForType(Transliterator t12r)
        => _composition.GetStringWithTransliterator(t12r);

    public string GetHiragana() => GetStringForType(Transliterator.Hiragana);
    public string GetFullKatakana() => GetStringForType(Transliterator.FullKatakana);
    public string GetHalfKatakana() => GetStringForType(Transliterator.HalfKatakana);
    public string GetFullAscii() => GetStringForType(Transliterator.FullAscii);
    public string GetHalfAscii() => GetStringForType(Transliterator.HalfAscii);

    public Composition Composition => _composition;
}
