using Mozc.Converter;
using Mozc.Engine;
using Mozc.Rewriter;

namespace Mozc.Session;

// C++ src/session/session_converter.cc の中核スライス。Composer(入力)→ Engine(変換)→
// 候補選択・確定の状態遷移を管理する。IPC/Output protobuf 生成は後続。
public sealed class SessionConverter
{
    public enum State { Composition, Conversion }

    private readonly MozcEngine _engine;
    private readonly IRewriter? _rewriter;
    private Composer.Composer _composer;

    private Segments? _segments;
    private int _focusedSegment;
    private int[] _selected = global::System.Array.Empty<int>();

    public SessionConverter(MozcEngine engine, IRewriter? rewriter = null)
    {
        _engine = engine;
        _rewriter = rewriter;
        _composer = engine.CreateComposer();
    }

    public State CurrentState { get; private set; } = State.Composition;
    public int FocusedSegment => _focusedSegment;

    // 入力(ローマ字 1 打鍵)。変換中なら一旦確定してから新規入力。
    public void InsertCharacter(string key)
    {
        if (CurrentState == State.Conversion)
        {
            Commit();
        }
        _composer.InsertCharacter(key);
    }

    // 未確定の preedit(変換中は選択中候補列、未変換時はかな preedit)。
    public string GetPreedit()
    {
        if (CurrentState == State.Composition)
        {
            return _composer.GetStringForPreedit();
        }
        var sb = new global::System.Text.StringBuilder();
        for (int i = 0; i < _segments!.ConversionSegmentsSize; i++)
        {
            sb.Append(_segments.ConversionSegment(i).Get(_selected[i]).Value);
        }
        return sb.ToString();
    }

    // スペース等で変換開始。
    public bool Convert()
    {
        string query = _composer.GetQueryForConversion();
        if (query.Length == 0)
        {
            return false;
        }
        _segments = _engine.Convert(query);
        _rewriter?.Rewrite(_segments);
        int n = _segments.ConversionSegmentsSize;
        if (n == 0)
        {
            _segments = null;
            return false;
        }
        _selected = new int[n];
        _focusedSegment = 0;
        CurrentState = State.Conversion;
        return true;
    }

    // 注目文節の次/前候補へ。
    public void ConvertNext() => MoveCandidate(+1);
    public void ConvertPrev() => MoveCandidate(-1);

    private void MoveCandidate(int delta)
    {
        if (CurrentState != State.Conversion)
        {
            return;
        }
        Segment seg = _segments!.ConversionSegment(_focusedSegment);
        int count = seg.CandidatesSize;
        if (count == 0)
        {
            return;
        }
        _selected[_focusedSegment] = ((_selected[_focusedSegment] + delta) % count + count) % count;
    }

    // 注目文節を右/左へ。
    public void SegmentFocusRight()
    {
        if (CurrentState == State.Conversion && _focusedSegment < _segments!.ConversionSegmentsSize - 1)
        {
            _focusedSegment++;
        }
    }

    public void SegmentFocusLeft()
    {
        if (CurrentState == State.Conversion && _focusedSegment > 0)
        {
            _focusedSegment--;
        }
    }

    // 確定文字列を返し、状態を初期化する。
    public string Commit()
    {
        string result = GetPreedit();
        Reset();
        return result;
    }

    // 変換を取り消して入力状態へ戻る(かなは保持)。
    public void Cancel()
    {
        _segments = null;
        CurrentState = State.Composition;
    }

    public void Reset()
    {
        _composer = _engine.CreateComposer();
        _segments = null;
        _selected = global::System.Array.Empty<int>();
        _focusedSegment = 0;
        CurrentState = State.Composition;
    }

    // 注目文節の候補一覧(候補ウィンドウ用)。
    public IReadOnlyList<string> GetCandidates()
    {
        if (CurrentState != State.Conversion)
        {
            return global::System.Array.Empty<string>();
        }
        Segment seg = _segments!.ConversionSegment(_focusedSegment);
        var list = new List<string>(seg.CandidatesSize);
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            list.Add(seg.Get(i).Value);
        }
        return list;
    }
}
