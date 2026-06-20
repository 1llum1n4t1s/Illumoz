using System.Buffers.Binary;
using System.Text;

namespace Mozc.Dictionary;

// C++ src/dictionary/user_dictionary_storage.cc 相当(中核スライス)。ユーザー登録単語を
// 保持し、読みの前方一致で引く。決定的バイナリで永続化する(user_dictionary.db 相当)。
// 変換/予測へ供給する自己完結ストア(外部データ不要)。
public sealed class UserDictionaryStorage
{
    // 1 エントリ。Reading=よみ, Word=表記, Pos=品詞, Comment=コメント。
    public sealed record UserEntry(string Reading, string Word, string Pos, string Comment);

    private const uint Magic = 0x4D5A5544; // "MZUD"
    private readonly List<UserEntry> _entries = new();

    public int Count => _entries.Count;
    public IReadOnlyList<UserEntry> Entries => _entries;

    // 追加(同一 Reading+Word は重複登録しない)。読み/単語/コメントの妥当性も検査する
    // (空・制御文字含み・長すぎは拒否。UserDictionaryUtil.ValidateEntry 準拠)。
    public bool Add(UserEntry entry)
    {
        if (UserDictionaryUtil.ValidateEntry(entry.Reading, entry.Word, entry.Comment)
            != UserDictionaryUtil.ValidationResult.Ok)
        {
            return false;
        }
        // 読みを正規化(全角→半角/半角カナ→全角カナ/カタカナ→ひらがな)してから格納する。
        // 変換時の照合キー(composer のひらがな)と一致させないと、カタカナ等で登録した語が
        // LookupExact("...") で引けず変換に出てこないため。
        string normalized = UserDictionaryUtil.NormalizeReading(entry.Reading);
        if (normalized != entry.Reading)
        {
            entry = entry with { Reading = normalized };
        }
        if (_entries.Exists(e => e.Reading == entry.Reading && e.Word == entry.Word))
        {
            return false;
        }
        _entries.Add(entry);
        return true;
    }

    public bool Remove(string reading, string word)
        => _entries.RemoveAll(e => e.Reading == reading && e.Word == word) > 0;

    // TSV(よみ\t単語\t品詞[\tコメント])を一括インポートする(辞書ツールの取込相当)。
    // 追加できた件数を返す(空行/#コメント/列不足/重複はスキップ)。
    public int ImportTsv(string tsv)
    {
        int added = 0;
        foreach (string line in tsv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (row.Length == 0 || row[0] == '#')
            {
                continue;
            }
            string[] f = row.Split('\t');
            if (f.Length < 3)
            {
                continue;
            }
            if (Add(new UserEntry(f[0], f[1], f[2], f.Length >= 4 ? f[3] : string.Empty)))
            {
                added++;
            }
        }
        return added;
    }

    // 全エントリを TSV へ書き出す(辞書ツールの書出相当)。
    public string ExportTsv()
    {
        var sb = new StringBuilder();
        foreach (UserEntry e in _entries)
        {
            sb.Append(e.Reading).Append('\t').Append(e.Word).Append('\t')
              .Append(e.Pos).Append('\t').Append(e.Comment).Append('\n');
        }
        return sb.ToString();
    }

    public void Clear() => _entries.Clear();

    // 読みの前方一致で引く(変換/予測供給用)。
    public List<UserEntry> LookupPredictive(string prefix)
    {
        var result = new List<UserEntry>();
        if (string.IsNullOrEmpty(prefix))
        {
            return result;
        }
        foreach (UserEntry e in _entries)
        {
            if (e.Reading.StartsWith(prefix, global::System.StringComparison.Ordinal))
            {
                result.Add(e);
            }
        }
        return result;
    }

    // 完全一致(変換用)。
    public List<UserEntry> LookupExact(string reading)
    {
        var result = new List<UserEntry>();
        foreach (UserEntry e in _entries)
        {
            if (e.Reading == reading)
            {
                result.Add(e);
            }
        }
        return result;
    }

    public byte[] Serialize()
    {
        // 決定的順: Reading→Word→Pos。
        var sorted = new List<UserEntry>(_entries);
        sorted.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Reading, b.Reading);
            if (c != 0) { return c; }
            c = string.CompareOrdinal(a.Word, b.Word);
            return c != 0 ? c : string.CompareOrdinal(a.Pos, b.Pos);
        });

        using var ms = new global::System.IO.MemoryStream();
        WriteU32(ms, Magic);
        WriteU32(ms, sorted.Count);
        foreach (UserEntry e in sorted)
        {
            WriteStr(ms, e.Reading);
            WriteStr(ms, e.Word);
            WriteStr(ms, e.Pos);
            WriteStr(ms, e.Comment);
        }
        return ms.ToArray();
    }

    // 保存は AtomicFile(temp→rename)で行い、保存中の異常終了でもユーザー辞書の全損を防ぐ。
    public void Save(string path) => Mozc.Base.AtomicFile.WriteAllBytes(path, Serialize());

    public bool Load(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic)
        {
            return false;
        }
        // 一旦ローカルへ全件読み切ってから _entries へ反映する。途中で破損していても
        // 部分的に読み込んだエントリが live 辞書に残らないようにする(変換へ影響させない)。
        var parsed = new List<UserEntry>();
        try
        {
            int pos = 4;
            int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
            pos += 4;
            // 巨大 count による過大アロケーション(破損/悪意ファイル)を弾く。
            // 最小エントリ = 空文字列4つ(各長さ4B)= 16B。
            if (count < 0 || count > (data.Length - pos) / 16)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                string reading = ReadStr(data, ref pos);
                string word = ReadStr(data, ref pos);
                string posTag = ReadStr(data, ref pos);
                string comment = ReadStr(data, ref pos);
                parsed.Add(new UserEntry(reading, word, posTag, comment));
            }
        }
        catch (global::System.Exception)
        {
            // 破損ファイル(境界外/長さ不正)で落とさず、既存 live 辞書も変えない。
            return false;
        }
        // 全件読めたので live 辞書を置き換える(Add で検証/正規化/重複排除を通す)。
        _entries.Clear();
        foreach (UserEntry e in parsed)
        {
            Add(e);
        }
        return true;
    }

    public bool LoadFile(string path)
        => global::System.IO.File.Exists(path) && Load(global::System.IO.File.ReadAllBytes(path));

    private static void WriteU32(global::System.IO.MemoryStream ms, uint v)
    {
        global::System.Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        ms.Write(b);
    }

    private static void WriteU32(global::System.IO.MemoryStream ms, int v) => WriteU32(ms, (uint)v);

    private static void WriteStr(global::System.IO.MemoryStream ms, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        WriteU32(ms, b.Length);
        ms.Write(b);
    }

    private static string ReadStr(ReadOnlySpan<byte> data, ref int pos)
    {
        int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
        pos += 4;
        string s = Encoding.UTF8.GetString(data.Slice(pos, len));
        pos += len;
        return s;
    }
}
