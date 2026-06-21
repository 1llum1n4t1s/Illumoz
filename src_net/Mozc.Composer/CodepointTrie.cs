using System.Text;

namespace Mozc.Composer;

// C++ base/container/trie.h の Trie<T> 相当。コードポイント単位のキー trie。
// LookUpPrefix の keyLength は UTF-16 code unit 長で返す(C++ は UTF-8 byte だが
// 常にコードポイント境界で消費するため、利用側の substring も同単位なら結果は同一)。
internal sealed class CodepointTrie<T> where T : class
{
    private readonly Dictionary<int, CodepointTrie<T>> _children = new();
    private T? _data;
    private bool _hasData;

    public void AddEntry(string key, T data)
    {
        if (key.Length == 0)
        {
            _data = data;
            _hasData = true;
            return;
        }
        SplitFirstChar(key, out int first, out string rest);
        if (!_children.TryGetValue(first, out CodepointTrie<T>? child))
        {
            child = new CodepointTrie<T>();
            _children[first] = child;
        }
        child.AddEntry(rest, data);
    }

    public T? LookUp(string key)
    {
        if (key.Length == 0)
        {
            return _hasData ? _data : null;
        }
        SplitFirstChar(key, out int first, out string rest);
        return _children.TryGetValue(first, out CodepointTrie<T>? child) ? child.LookUp(rest) : null;
    }

    // trie.h LookUpPrefix を忠実移植。
    public T? LookUpPrefix(string key, out int keyLength, out bool isFixed)
    {
        CodepointTrie<T>? child = null;
        int firstCharLen = 0;
        string rest = string.Empty;
        if (key.Length != 0)
        {
            SplitFirstChar(key, out int first, out rest);
            firstCharLen = key.Length - rest.Length;
            _children.TryGetValue(first, out child);
        }

        if (child == null)
        {
            keyLength = 0;
            if (_hasData)
            {
                isFixed = _children.Count == 0;
                return _data;
            }
            isFixed = true;
            return null;
        }

        T? found = child.LookUpPrefix(rest, out keyLength, out isFixed);
        keyLength += firstCharLen;
        return found;
    }

    public void LookUpPredictiveAll(string key, List<T> dataList)
    {
        if (key.Length != 0)
        {
            SplitFirstChar(key, out int first, out string rest);
            if (_children.TryGetValue(first, out CodepointTrie<T>? child))
            {
                child.LookUpPredictiveAll(rest, dataList);
            }
            return;
        }
        if (_hasData && _data != null)
        {
            dataList.Add(_data);
        }
        foreach (CodepointTrie<T> child in _children.Values)
        {
            child.LookUpPredictiveAll(string.Empty, dataList);
        }
    }

    public bool HasSubTrie(string key)
    {
        if (key.Length == 0)
        {
            return false; // C++ FindSubTrie(empty)→nullptr→false
        }
        SplitFirstChar(key, out int first, out string rest);
        if (!_children.TryGetValue(first, out CodepointTrie<T>? child))
        {
            return false;
        }
        return rest.Length == 0 || child.HasSubTrie(rest);
    }

    private static void SplitFirstChar(string s, out int first, out string rest)
    {
        Rune.DecodeFromUtf16(s, out Rune rune, out int consumed);
        first = rune.Value;
        rest = s.Substring(consumed);
    }
}
