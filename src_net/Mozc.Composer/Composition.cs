using System.Text;

namespace Mozc.Composer;

public enum TrimMode
{
    Trim, // 確定値のみ
    Fix,  // 未確定も確定扱い
    Asis, // そのまま
}

// C++ composer::Composition 相当(末尾追記の主要フローに対応)。
// CharChunk のリストを管理し、入力を trie で変換、preedit/確定文字列を生成する。
public sealed class Composition
{
    private readonly Table _table;
    private readonly List<CharChunk> _chunks = new();

    public Composition(Table table)
    {
        _table = table;
    }

    public void Erase() => _chunks.Clear();

    public IReadOnlyList<CharChunk> Chunks => _chunks;

    // 末尾に input を追記する(C++ InsertInput の pos=末尾ケース)。
    public void InsertInput(CompositionInput input)
    {
        if (input.Empty)
        {
            return;
        }

        int leftIndex = GetInsertionChunkAtEnd();
        CombinePendingChunks(leftIndex, input);
        CharChunk left = _chunks[leftIndex];

        while (true)
        {
            left.AddCompositionInput(input);
            if (input.Empty)
            {
                break;
            }
            left = new CharChunk(_table);
            _chunks.Add(left);
            leftIndex = _chunks.Count - 1;
            input.IsNewInput = false;
        }

        if (left.IsEmpty)
        {
            _chunks.RemoveAt(_chunks.IndexOf(left));
        }
    }

    public void InsertAt(string input) => InsertInput(CompositionInput.FromRaw(input));

    // 末尾の挿入先 chunk のインデックスを返す(無ければ新規追加)。
    private int GetInsertionChunkAtEnd()
    {
        if (_chunks.Count == 0)
        {
            _chunks.Add(new CharChunk(_table));
            return 0;
        }
        CharChunk last = _chunks[^1];
        if (last.IsAppendable(_table))
        {
            return _chunks.Count - 1;
        }
        _chunks.Add(new CharChunk(_table));
        return _chunks.Count - 1;
    }

    private void CombinePendingChunks(int index, CompositionInput input)
    {
        if (input.IsAsis)
        {
            return;
        }
        string nextInput = input.Conversion.Length == 0 ? input.Raw : input.Conversion;

        while (index > 0)
        {
            CharChunk cur = _chunks[index];
            CharChunk left = _chunks[index - 1];
            if (!left.IsConvertible(_table, cur.Pending + nextInput))
            {
                return;
            }
            cur.Combine(left);
            _chunks.RemoveAt(index - 1);
            index--;
        }
    }

    public string GetString()
    {
        var sb = new StringBuilder();
        foreach (CharChunk chunk in _chunks)
        {
            chunk.AppendResult(sb);
        }
        return sb.ToString();
    }

    public string GetStringWithTrimMode(TrimMode trimMode)
    {
        var sb = new StringBuilder();
        if (_chunks.Count == 0)
        {
            return string.Empty;
        }
        for (int i = 0; i < _chunks.Count - 1; i++)
        {
            _chunks[i].AppendResult(sb);
        }
        CharChunk last = _chunks[^1];
        switch (trimMode)
        {
            case TrimMode.Trim:
                last.AppendTrimedResult(sb);
                break;
            case TrimMode.Fix:
                last.AppendFixedResult(sb);
                break;
            default:
                last.AppendResult(sb);
                break;
        }
        return sb.ToString();
    }

    public bool ShouldCommit()
    {
        foreach (CharChunk chunk in _chunks)
        {
            if (!chunk.ShouldCommit)
            {
                return false;
            }
        }
        return true;
    }
}
