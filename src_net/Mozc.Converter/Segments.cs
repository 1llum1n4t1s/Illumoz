namespace Mozc.Converter;

// C++ src/converter/segments.h の Segments 相当。Segment の配列。
// 先頭から HISTORY/SUBMITTED が続く区間が「履歴セグメント」、以降が「変換セグメント」。
public sealed class Segments
{
    private const int MaxHistorySize = 32;

    private readonly List<Segment> _segments = new();
    private int _maxHistorySegmentsSize;

    public bool Resized { get; set; }

    public int MaxHistorySegmentsSize => _maxHistorySegmentsSize;

    public void SetMaxHistorySegmentsSize(int size)
        => _maxHistorySegmentsSize = global::System.Math.Clamp(size, 0, MaxHistorySize);

    // --- 全セグメント ---
    public int SegmentsSize => _segments.Count;
    public IReadOnlyList<Segment> All => _segments;
    public Segment GetSegment(int i) => _segments[i];
    public Segment MutableSegment(int i) => _segments[i];

    public Segment PushBackSegment()
    {
        var s = new Segment();
        _segments.Add(s);
        return s;
    }

    public Segment AddSegment() => PushBackSegment();

    public Segment PushFrontSegment()
    {
        var s = new Segment();
        _segments.Insert(0, s);
        return s;
    }

    public Segment InsertSegment(int i)
    {
        var s = new Segment();
        _segments.Insert(i, s);
        return s;
    }

    public void PopFrontSegment() => _segments.RemoveAt(0);
    public void PopBackSegment() => _segments.RemoveAt(_segments.Count - 1);
    public void EraseSegment(int i) => _segments.RemoveAt(i);
    public void EraseSegments(int i, int size) => _segments.RemoveRange(i, size);
    public void ClearSegments() => _segments.Clear();

    // --- 履歴/変換セグメント ---
    public int HistorySegmentsSize
    {
        get
        {
            int i = 0;
            while (i < _segments.Count &&
                   (_segments[i].Type is Segment.SegmentType.History or Segment.SegmentType.Submitted))
            {
                i++;
            }
            return i;
        }
    }

    public int ConversionSegmentsSize => SegmentsSize - HistorySegmentsSize;

    public Segment HistorySegment(int i) => _segments[i];
    public Segment MutableHistorySegment(int i) => _segments[i];
    public Segment ConversionSegment(int i) => _segments[i + HistorySegmentsSize];
    public Segment MutableConversionSegment(int i) => _segments[i + HistorySegmentsSize];

    public IEnumerable<Segment> HistorySegments => _segments.Take(HistorySegmentsSize);
    public IEnumerable<Segment> ConversionSegments => _segments.Skip(HistorySegmentsSize);

    public void ClearHistorySegments()
    {
        int n = HistorySegmentsSize;
        if (n > 0)
        {
            _segments.RemoveRange(0, n);
        }
    }

    public void ClearConversionSegments()
    {
        int start = HistorySegmentsSize;
        _segments.RemoveRange(start, _segments.Count - start);
    }

    public void Clear()
    {
        _segments.Clear();
        _maxHistorySegmentsSize = 0;
        Resized = false;
    }
}
