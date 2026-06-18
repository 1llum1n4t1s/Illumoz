using System.Text;

namespace Mozc.Converter;

// C++ src/converter/segments.h の Segment 相当。1 文節(キー + 候補列 + メタ候補)。
// C++ の pool/deque は C# では List + GC に置換。
public sealed class Segment
{
    public enum SegmentType
    {
        Free,           // 全自動変換
        FixedBoundary,  // 複数文節に分割不可
        FixedValue,     // 分割不可かつ結果も固定
        Submitted,      // 確定済みノード
        History,         // 履歴ノード(ユーザーには非表示)
    }

    private readonly List<Candidate> _candidates = new();
    private readonly List<Candidate> _metaCandidates = new();
    private string _key = string.Empty;

    public SegmentType Type { get; set; } = SegmentType.Free;

    public string Key => _key;
    public int KeyLen { get; private set; }

    public void SetKey(string key)
    {
        _key = key;
        KeyLen = CharsLen(key);
    }

    // --- 候補操作 ---
    public int CandidatesSize => _candidates.Count;
    public IReadOnlyList<Candidate> Candidates => _candidates;
    public Candidate Get(int i) => _candidates[i];
    public Candidate Mutable(int i) => _candidates[i];
    public bool IsValidIndex(int i) =>
        i >= 0 ? i < _candidates.Count : -i - 1 < _metaCandidates.Count;

    public Candidate PushBackCandidate()
    {
        var c = new Candidate();
        _candidates.Add(c);
        return c;
    }

    public Candidate AddCandidate() => PushBackCandidate();

    public Candidate PushFrontCandidate()
    {
        var c = new Candidate();
        _candidates.Insert(0, c);
        return c;
    }

    public Candidate InsertCandidate(int i)
    {
        var c = new Candidate();
        _candidates.Insert(i, c);
        return c;
    }

    public void InsertCandidate(int i, Candidate candidate) => _candidates.Insert(i, candidate);

    public void InsertCandidates(int i, IEnumerable<Candidate> candidates)
        => _candidates.InsertRange(i, candidates);

    public void PopFrontCandidate() => _candidates.RemoveAt(0);
    public void PopBackCandidate() => _candidates.RemoveAt(_candidates.Count - 1);
    public void EraseCandidate(int i) => _candidates.RemoveAt(i);
    public void EraseCandidates(int i, int size) => _candidates.RemoveRange(i, size);
    public void ClearCandidates() => _candidates.Clear();

    public void MoveCandidate(int oldIdx, int newIdx)
    {
        if (oldIdx == newIdx)
        {
            return;
        }
        Candidate c = _candidates[oldIdx];
        _candidates.RemoveAt(oldIdx);
        _candidates.Insert(newIdx, c);
    }

    // --- メタ候補(変換形バリエーション等) ---
    public int MetaCandidatesSize => _metaCandidates.Count;
    public IReadOnlyList<Candidate> MetaCandidates => _metaCandidates;
    public Candidate MetaCandidate(int i) => _metaCandidates[i];
    public Candidate AddMetaCandidate()
    {
        var c = new Candidate();
        _metaCandidates.Add(c);
        return c;
    }
    public void ClearMetaCandidates() => _metaCandidates.Clear();

    public void Clear()
    {
        _key = string.Empty;
        KeyLen = 0;
        Type = SegmentType.Free;
        _candidates.Clear();
        _metaCandidates.Clear();
    }

    // コードポイント数(C++ Util::CharsLen)。
    private static int CharsLen(string s)
    {
        int count = 0;
        foreach (Rune _ in s.EnumerateRunes())
        {
            count++;
        }
        return count;
    }
}
