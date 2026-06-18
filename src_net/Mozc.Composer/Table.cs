using System.Text;

namespace Mozc.Composer;

// C++ src/composer/table.h の TableAttribute 相当。Entry の付加属性ビットマップ。
[Flags]
public enum TableAttributes : uint
{
    NoTableAttribute = 0,
    // 入力開始時、他規則の一部になり得ても、この属性の規則が実行される。
    NewChunk = 1,
    // CharChunk の transliteration を抑制し、as-is キーとして扱う。
    NoTransliteration = 2,
    // composition を終了し commit すべきことを示す。
    DirectInput = 4,
    // 次の入力を新規入力として扱う(NewChunk と併用)。
    EndChunk = 8,
}

// C++ composer::Entry 相当。input(キー入力)→ result(確定文字列) + pending(残り)。
public sealed class Entry
{
    public Entry(string input, string result, string pending, TableAttributes attributes)
    {
        Input = input;
        Result = result;
        Pending = pending;
        Attributes = attributes;
    }

    public string Input { get; }
    public string Result { get; }
    public string Pending { get; }
    public TableAttributes Attributes { get; }
}

// C++ composer::Table 相当。ローマ字/かな変換の trie テーブル。
// trie はコードポイント単位(C++ は UTF-8 byte 単位だが、常に 1 コードポイント境界で
// 分割するため C# の UTF-16 code unit 長で一貫させても結果は同一)。
public sealed class Table
{
    // kNewChunkPrefix: C++ では制御文字 \t を使う(table.cc)。入力に現れない印として流用。
    private const string NewChunkPrefix = "\t";

    private readonly CodepointTrie<Entry> _entries = new();
    private bool _caseSensitive;

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set => _caseSensitive = value;
    }

    public Entry? AddRule(string input, string output, string pending)
        => AddRuleWithAttributes(input, output, pending, TableAttributes.NoTableAttribute);

    public Entry? AddRuleWithAttributes(
        string input, string output, string pending, TableAttributes attributes)
    {
        if (attributes.HasFlag(TableAttributes.NewChunk))
        {
            AddRuleWithAttributes(NewChunkPrefix + input, output, pending,
                TableAttributes.NoTableAttribute);
        }

        const int maxSize = 300;
        if (input.Length >= maxSize || output.Length >= maxSize || pending.Length >= maxSize)
        {
            return null;
        }

        if (IsLoopingEntry(input, pending))
        {
            return null;
        }

        var entry = new Entry(input, output, pending, attributes);
        _entries.AddEntry(input, entry);

        // 大文字を含むなら case_sensitive 化(C++ と同じく一度 true になったら戻さない)。
        if (!_caseSensitive)
        {
            foreach (Rune r in input.EnumerateRunes())
            {
                if (r.Value is >= 'A' and <= 'Z')
                {
                    _caseSensitive = true;
                    break;
                }
            }
        }
        return entry;
    }

    // input→pending が変換規則のループを作るなら true。
    public bool IsLoopingEntry(string input, string pending)
    {
        if (pending.Length == 0)
        {
            return false;
        }
        string key = pending;
        while (true)
        {
            Entry? entry = LookUp(key);
            if (entry == null)
            {
                return false;
            }
            if (entry.Input == input && entry.Pending == pending)
            {
                return true;
            }
            if (entry.Pending.Length == 0)
            {
                return false;
            }
            // pending を辿る。input と一致したらループ。
            if (entry.Pending == key)
            {
                return false;
            }
            key = entry.Pending;
        }
    }

    public bool LoadFromString(string str)
    {
        foreach (string rawLine in str.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }
            string[] rules = line.Split('\t');
            if (rules.Length == 4)
            {
                AddRuleWithAttributes(rules[0], rules[1], rules[2], ParseAttributes(rules[3]));
            }
            else if (rules.Length == 3)
            {
                AddRule(rules[0], rules[1], rules[2]);
            }
            else if (rules.Length == 2)
            {
                AddRule(rules[0], rules[1], string.Empty);
            }
            else if (line[0] != '#')
            {
                // フォーマットエラーは無視(C++ は LOG(ERROR))。
            }
        }
        return true;
    }

    private static TableAttributes ParseAttributes(string input)
    {
        var attributes = TableAttributes.NoTableAttribute;
        foreach (string s in input.Split(' '))
        {
            attributes |= s switch
            {
                "NewChunk" => TableAttributes.NewChunk,
                "NoTransliteration" => TableAttributes.NoTransliteration,
                "DirectInput" => TableAttributes.DirectInput,
                "EndChunk" => TableAttributes.EndChunk,
                _ => TableAttributes.NoTableAttribute,
            };
        }
        return attributes;
    }

    public Entry? LookUp(string input)
    {
        string key = _caseSensitive ? input : input.ToLowerInvariant();
        return _entries.LookUp(key);
    }

    // 前方一致。keyLength は消費した input の長さ(UTF-16 code unit)、
    // fixed は「これ以上長い規則が無い確定一致」を示す。
    public Entry? LookUpPrefix(string input, out int keyLength, out bool isFixed)
    {
        string key = _caseSensitive ? input : input.ToLowerInvariant();
        return _entries.LookUpPrefix(key, out keyLength, out isFixed);
    }

    public List<Entry> LookUpPredictiveAll(string input)
    {
        string key = _caseSensitive ? input : input.ToLowerInvariant();
        var results = new List<Entry>();
        _entries.LookUpPredictiveAll(key, results);
        return results;
    }

    public bool HasSubRules(string input)
    {
        string key = _caseSensitive ? input : input.ToLowerInvariant();
        return _entries.HasSubTrie(key);
    }

    public bool HasNewChunkEntry(string input)
    {
        if (input.Length == 0)
        {
            return false;
        }
        LookUpPrefix(NewChunkPrefix + input, out int keyLength, out _);
        return keyLength > 1;
    }

    // 特殊キー({...})は現状未対応(romanji-hiragana.tsv は使わない)。恒等で返す。
    public string ParseSpecialKey(string input) => input;
}
