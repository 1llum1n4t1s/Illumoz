using System.Text;

namespace Mozc.Storage.Louds;

// C++ src/storage/louds/louds_trie_builder.{h,cc} 相当。
// キー集合(UTF-8 バイト列, バイト辞書順 sort + unique)から BFS で LOUDS trie 画像を構築。
// key id は BFS(深さ→位置)順に 0 から付与され、LoudsTrie.ExactSearch の Rank1 と一致する。
// C6(辞書データ生成)でも使用。
public sealed class LoudsTrieBuilder
{
    private static readonly ByteArrayComparer Comparer = new();

    private readonly List<byte[]> _words = new();
    private List<byte[]> _sorted = new();
    private int[] _idList = Array.Empty<int>();
    private bool _built;

    public void Add(string word) => Add(Encoding.UTF8.GetBytes(word));

    public void Add(byte[] word)
    {
        if (_built)
        {
            throw new InvalidOperationException("already built");
        }
        if (word.Length == 0)
        {
            throw new ArgumentException("empty word", nameof(word));
        }
        _words.Add(word);
    }

    public byte[] Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("already built");
        }

        // バイト辞書順に sort + unique。
        _sorted = _words.ToList();
        _sorted.Sort(Comparer);
        var dedup = new List<byte[]>(_sorted.Count);
        for (int i = 0; i < _sorted.Count; i++)
        {
            if (i == 0 || Comparer.Compare(_sorted[i], _sorted[i - 1]) != 0)
            {
                dedup.Add(_sorted[i]);
            }
        }
        _sorted = dedup;

        int count = _sorted.Count;
        _idList = new int[count];
        Array.Fill(_idList, -1);

        // entry_list: (word, original index = sorted index)
        var entries = new List<(byte[] Word, int Original)>(count);
        for (int i = 0; i < count; i++)
        {
            entries.Add((_sorted[i], i));
        }

        var trie = new BitStream();
        var terminal = new BitStream();
        var edgeChar = new List<byte> { 0 }; // '\0'

        trie.PushBit(1);
        trie.PushBit(0);
        terminal.PushBit(0);

        int id = 0;
        for (int depth = 0; entries.Count > 0; depth++)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                byte[] word = entries[i].Word;
                if (word.Length > depth &&
                    (i == 0 || !SubstrEqual(word, entries[i - 1].Word, depth + 1)))
                {
                    trie.PushBit(1);
                    edgeChar.Add(word[depth]);
                    if (word.Length == depth + 1)
                    {
                        terminal.PushBit(1);
                        _idList[entries[i].Original] = id;
                        id++;
                    }
                    else
                    {
                        terminal.PushBit(0);
                    }
                }
                if (i == entries.Count - 1 || !SubstrEqual(word, entries[i + 1].Word, depth))
                {
                    trie.PushBit(0);
                }
            }
            entries.RemoveAll(e => e.Word.Length < depth + 1);
        }

        trie.FillPadding32();
        terminal.FillPadding32();

        var image = new List<byte>();
        BitStream.PushInt32(image, (uint)trie.ByteSize);
        BitStream.PushInt32(image, (uint)terminal.ByteSize);
        BitStream.PushInt32(image, 8);
        BitStream.PushInt32(image, (uint)edgeChar.Count);
        trie.CopyTo(image);
        terminal.CopyTo(image);
        image.AddRange(edgeChar);

        _built = true;
        return image.ToArray();
    }

    // sort 後の key id を返す(未登録は -1)。LoudsTrie.ExactSearch の返り値と一致する。
    public int GetId(byte[] word)
    {
        if (!_built)
        {
            throw new InvalidOperationException("not built");
        }
        int lo = 0, hi = _sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (Comparer.Compare(_sorted[mid], word) < 0) lo = mid + 1;
            else hi = mid;
        }
        if (lo < _sorted.Count && Comparer.Compare(_sorted[lo], word) == 0)
        {
            return _idList[lo];
        }
        return -1;
    }

    public int GetId(string word) => GetId(Encoding.UTF8.GetBytes(word));

    // C++ std::string::compare(0, len, other, 0, len) == 0 相当(長さ制限つき部分一致)。
    private static bool SubstrEqual(byte[] a, byte[] b, int len)
    {
        int la = Math.Min(len, a.Length);
        int lb = Math.Min(len, b.Length);
        if (la != lb)
        {
            return false;
        }
        for (int i = 0; i < la; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
            => x.AsSpan().SequenceCompareTo(y.AsSpan());
    }
}
