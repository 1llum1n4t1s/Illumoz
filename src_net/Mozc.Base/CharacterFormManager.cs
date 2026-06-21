using System.Collections.Generic;
using System.Text;

namespace Mozc.Base;

// C++ src/config/character_form_manager.cc の中核スライス。
// スクリプト種別(数字/英字/カタカナ/記号)ごとに全角/半角の好みを保持し、
// 文字列を好みに沿って変換する。LAST_FORM(履歴記憶)とストレージ連携は将来対応。
// 文字種判定は既存の ScriptClassifier(ScriptType)を流用する。
public enum CharacterForm
{
    NoConversion = 0,
    FullWidth = 1,
    HalfWidth = 2,
    LastForm = 3, // ユーザーが最後に選んだ形を記憶して使う(履歴連動)。
}

public sealed class CharacterFormManager
{
    // 正規化代表文字(数字→'0'、英字→'A'、カタカナ→'ア'、記号は半角1文字)ごとの好み。
    private readonly Dictionary<char, CharacterForm> _table = new();

    // LAST_FORM 指定のグループについて、ユーザーが最後に選んだ形を記憶する。
    private readonly Dictionary<char, CharacterForm> _lastFormStorage = new();

    // C++ Preedit の既定ルール(全部 FULL_WIDTH)を構築した manager。
    public static CharacterFormManager CreatePreeditDefault()
    {
        var m = new CharacterFormManager();
        m.ApplyPreeditDefaultRules();
        return m;
    }

    private void ApplyPreeditDefaultRules()
    {
        AddRule("ア", CharacterForm.FullWidth);
        AddRule("A", CharacterForm.FullWidth);
        AddRule("0", CharacterForm.FullWidth);
        AddRule("(){}[]", CharacterForm.FullWidth);
        AddRule(".,", CharacterForm.FullWidth);
        AddRule("。、", CharacterForm.FullWidth);
        AddRule("・「」", CharacterForm.FullWidth);
        AddRule("\"'", CharacterForm.FullWidth);
        AddRule(":;", CharacterForm.FullWidth);
        AddRule("#%&@$^_|`\\", CharacterForm.FullWidth);
        AddRule("~", CharacterForm.FullWidth);
        AddRule("<>=+-/*", CharacterForm.FullWidth);
        AddRule("?!", CharacterForm.FullWidth);
    }

    // 変換結果用の既定マネージャ(C++ character_form_manager.cc の conversion 既定)。
    // preedit 既定は全て FULL_WIDTH だが、変換側は ASCII/数字/記号を LAST_FORM(直近選択を記憶)に
    // して、半角を選べば半角が、全角を選べば全角が以後も使われるようにする。かな/句読点は FULL_WIDTH。
    public static CharacterFormManager CreateConversionDefault()
    {
        var m = new CharacterFormManager();
        m.ApplyConversionDefaultRules();
        return m;
    }

    private void ApplyConversionDefaultRules()
    {
        AddRule("ア", CharacterForm.FullWidth);
        AddRule("A", CharacterForm.LastForm);
        AddRule("0", CharacterForm.LastForm);
        AddRule("(){}[]", CharacterForm.LastForm);
        AddRule(".,", CharacterForm.LastForm);
        AddRule("。、", CharacterForm.FullWidth);
        AddRule("・「」", CharacterForm.FullWidth);
        AddRule("\"'", CharacterForm.LastForm);
        AddRule(":;", CharacterForm.LastForm);
        AddRule("#%&@$^_|`\\", CharacterForm.LastForm);
        AddRule("~", CharacterForm.LastForm);
        AddRule("<>=+-/*", CharacterForm.LastForm);
        AddRule("?!", CharacterForm.LastForm);
    }

    // config.character_form_rules 相当(group 文字列 + form)からまとめて構築する。
    // C++ では LAST_FORM は履歴記憶だが、未実装のため呼び出し側で FullWidth 等へ
    // 解決した form を渡す。空 group / 空 rules はそのまま無視される。
    public static CharacterFormManager FromRules(
        global::System.Collections.Generic.IEnumerable<(string Group, CharacterForm Form)> rules)
    {
        var m = new CharacterFormManager();
        m.ApplyRules(rules);
        return m;
    }

    private void ApplyRules(
        global::System.Collections.Generic.IEnumerable<(string Group, CharacterForm Form)> rules)
    {
        foreach ((string group, CharacterForm form) in rules)
        {
            if (!string.IsNullOrEmpty(group))
            {
                AddRule(group, form);
            }
        }
    }

    // --- in-place ルール再構築(C++ CharacterFormManager::Clear 相当) ---
    // ルール表(_table)だけを作り直し、LAST_FORM 学習(_lastFormStorage)は保持する。
    // config 変更のたびにインスタンスを new し直すと、character_form.db から読んだ・入力中に
    // 学習した半/全角の好みが捨てられ、保存時に空履歴で上書きされるため(無関係な設定変更で
    // 学習が消える不具合)、同一インスタンスのルールだけ差し替える。C++ も Clear() はルール
    // table のみクリアし storage_(履歴)は残す。
    public void ReloadPreeditDefault()
    {
        _table.Clear();
        ApplyPreeditDefaultRules();
    }

    public void ReloadConversionDefault()
    {
        _table.Clear();
        ApplyConversionDefaultRules();
    }

    public void ReloadRules(
        global::System.Collections.Generic.IEnumerable<(string Group, CharacterForm Form)> rules)
    {
        _table.Clear();
        ApplyRules(rules);
    }

    // input の各文字を正規化代表へ畳んで form を登録する。
    public void AddRule(string input, CharacterForm form)
    {
        foreach (char c in input)
        {
            char norm = NormalizedCharacter(c.ToString());
            if (norm != '\0')
            {
                _table[norm] = form;
            }
        }
    }

    // 文字列(または1文字)の好みを返す。判定不能は NoConversion。
    // ルールが LAST_FORM のグループは記憶された形(既定 FullWidth)を返す。
    public CharacterForm GetCharacterForm(string str)
    {
        char norm = NormalizedCharacter(str);
        if (norm == '\0')
        {
            return CharacterForm.NoConversion;
        }
        if (!_table.TryGetValue(norm, out CharacterForm f))
        {
            return CharacterForm.NoConversion;
        }
        if (f == CharacterForm.LastForm)
        {
            return _lastFormStorage.TryGetValue(norm, out CharacterForm stored)
                ? stored
                : CharacterForm.FullWidth; // 既定。
        }
        return f;
    }

    // ルールが LAST_FORM のグループについて、選択された形を記憶する(C++ SetCharacterForm)。
    public void SetCharacterForm(string str, CharacterForm form)
    {
        if (form != CharacterForm.FullWidth && form != CharacterForm.HalfWidth)
        {
            return;
        }
        char norm = NormalizedCharacter(str);
        if (norm == '\0' || !_table.TryGetValue(norm, out CharacterForm rule))
        {
            return;
        }
        if (rule == CharacterForm.LastForm)
        {
            _lastFormStorage[norm] = form;
        }
    }

    // str の実際の字幅を判定し、その形を LAST_FORM 記憶へ反映する(C++ GuessAndSetCharacterForm)。
    public void GuessAndSetCharacterForm(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return;
        }
        bool hasFull = false;
        bool hasHalf = false;
        foreach (Rune rune in str.EnumerateRunes())
        {
            int cp = rune.Value;
            // 全角 ASCII / 全角カタカナ → 全角、半角 ASCII / 半角カタカナ → 半角(ざっくり判定)。
            if ((cp >= 0xFF01 && cp <= 0xFF5E)      // 全角 ASCII 記号英数
                || (cp >= 0x30A0 && cp <= 0x30FF))  // 全角カタカナ
            {
                hasFull = true;
            }
            else if ((cp >= 0x21 && cp <= 0x7E)        // 半角 ASCII 記号英数
                     || (cp >= 0xFF61 && cp <= 0xFF9F)) // 半角カタカナ
            {
                hasHalf = true;
            }
        }
        if (hasFull && !hasHalf)
        {
            SetCharacterForm(str, CharacterForm.FullWidth);
        }
        else if (hasHalf && !hasFull)
        {
            SetCharacterForm(str, CharacterForm.HalfWidth);
        }
    }

    // LAST_FORM 記憶をクリアする(C++ ClearHistory)。
    public void ClearHistory() => _lastFormStorage.Clear();

    // --- LAST_FORM 記憶の永続化(C++ LruStorage 相当の最小実装) ---
    private static readonly byte[] Magic = { (byte)'M', (byte)'Z', (byte)'C', (byte)'F' };

    // 記憶を決定的バイナリへ。[magic][u32 count]{[u16 char][u8 form]}(char 昇順)。
    public byte[] SerializeHistory()
    {
        using var ms = new global::System.IO.MemoryStream();
        using var w = new global::System.IO.BinaryWriter(ms);
        w.Write(Magic);
        var keys = new List<char>(_lastFormStorage.Keys);
        keys.Sort();
        w.Write((uint)keys.Count);
        foreach (char k in keys)
        {
            w.Write((ushort)k);
            w.Write((byte)_lastFormStorage[k]);
        }
        w.Flush();
        return ms.ToArray();
    }

    // SerializeHistory のバイト列を読み込む。magic 不一致は false(記憶は変更しない)。
    public bool DeserializeHistory(byte[] data)
    {
        if (data.Length < 8)
        {
            return false;
        }
        for (int i = 0; i < Magic.Length; i++)
        {
            if (data[i] != Magic[i])
            {
                return false;
            }
        }
        try
        {
            using var ms = new global::System.IO.MemoryStream(data);
            using var r = new global::System.IO.BinaryReader(ms);
            r.ReadBytes(Magic.Length);
            uint count = r.ReadUInt32();
            // 1エントリ=3バイト(UInt16+Byte)。残量不足の破損データを早期に弾く。
            if (ms.Length - ms.Position < (long)count * 3)
            {
                return false;
            }
            _lastFormStorage.Clear();
            for (uint i = 0; i < count; i++)
            {
                char k = (char)r.ReadUInt16();
                var form = (CharacterForm)r.ReadByte();
                _lastFormStorage[k] = form;
            }
            return true;
        }
        catch (global::System.IO.EndOfStreamException)
        {
            // 破損履歴で落とさず学習なしとして継続。
            return false;
        }
    }

    // 保存は AtomicFile(temp→rename)で行い、保存中の異常終了でも LAST_FORM 記憶の全損を防ぐ。
    public void SaveHistory(string path)
        => AtomicFile.WriteAllBytes(path, SerializeHistory());

    // ファイルが無ければ何もせず false。
    public bool LoadHistory(string path)
        => global::System.IO.File.Exists(path) && DeserializeHistory(global::System.IO.File.ReadAllBytes(path));

    // str を好みに沿って全角/半角変換する(C++ TryConvertStringWithPreference 相当)。
    // 連続する同 form のランをまとめて変換する。
    public string ConvertString(string str)
    {
        var output = new StringBuilder();
        var buf = new StringBuilder();
        CharacterForm prevForm = CharacterForm.NoConversion;
        ScriptType prevType = ScriptType.Unknown;
        bool first = true;

        foreach (Rune rune in str.EnumerateRunes())
        {
            string ch = rune.ToString();
            ScriptType type = ScriptClassifier.Classify(rune.Value);
            // 半角の濁点(U+FF9E)/半濁点(U+FF9F)はカタカナ列の続き扱いにする。Other のまま
            // だと "ｶﾞ" が "ｶ" と分断され、幅変換が結合できず "カﾞ" になってしまう。続けて
            // バッファに残せば幅変換表が "ｶﾞ"→"ガ" を結合できる。
            if ((rune.Value == 0xFF9E || rune.Value == 0xFF9F) && prevType == ScriptType.Katakana)
            {
                type = ScriptType.Katakana;
            }
            CharacterForm form = prevForm;

            // 記号(Other)は C++ の UNKNOWN_SCRIPT 相当: 毎回 GetCharacterForm。
            // Katakana/Numeric/Alphabet は種別が変わった所で再計算。
            if (type == ScriptType.Other
                || (type == ScriptType.Katakana && prevType != ScriptType.Katakana)
                || (type == ScriptType.Numeric && prevType != ScriptType.Numeric)
                || (type == ScriptType.Alphabet && prevType != ScriptType.Alphabet))
            {
                form = GetCharacterForm(ch);
            }
            else if (type == ScriptType.Kanji || type == ScriptType.Hiragana)
            {
                form = CharacterForm.NoConversion;
            }

            if (!first && prevForm != form)
            {
                output.Append(ConvertWidth(buf.ToString(), prevForm));
                buf.Clear();
            }

            buf.Append(ch);
            prevType = type;
            prevForm = form;
            first = false;
        }

        if (buf.Length > 0)
        {
            output.Append(ConvertWidth(buf.ToString(), prevForm));
        }
        return output.ToString();
    }

    // C++ CharacterFormManager::ConvertWidth 相当。
    public static string ConvertWidth(string input, CharacterForm form) => form switch
    {
        CharacterForm.FullWidth => JapaneseUtil.HalfWidthToFullWidth(input),
        CharacterForm.HalfWidth => JapaneseUtil.FullWidthToHalfWidth(input),
        _ => input,
    };

    // C++ GetNormalizedCharacter 相当。カタカナ→'ア'、数字→'0'、英字→'A'、
    // 記号は半角化した1文字、それ以外は '\0'(変換なし)。
    private static char NormalizedCharacter(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return '\0';
        }
        Rune first = default;
        int count = 0;
        foreach (Rune r in str.EnumerateRunes())
        {
            if (count == 0)
            {
                first = r;
            }
            count++;
        }
        switch (ScriptClassifier.Classify(first.Value))
        {
            case ScriptType.Katakana: return 'ア';
            case ScriptType.Numeric: return '0';
            case ScriptType.Alphabet: return 'A';
            case ScriptType.Kanji:
            case ScriptType.Hiragana: return '\0';
            default: // 記号(Other): 1文字のみ正規化
                if (count != 1)
                {
                    return '\0';
                }
                string half = JapaneseUtil.FullWidthToHalfWidth(str);
                return half.Length >= 1 && half[0] <= 0xFFFF ? half[0] : '\0';
        }
    }
}
