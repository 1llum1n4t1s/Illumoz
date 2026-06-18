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
    private readonly Prediction.UserHistoryPredictor? _history;
    private Composer.Composer _composer;

    private Segments? _segments;
    private int _focusedSegment;
    private int[] _selected = global::System.Array.Empty<int>();

    public SessionConverter(MozcEngine engine, IRewriter? rewriter = null,
        Prediction.UserHistoryPredictor? history = null)
    {
        _engine = engine;
        _rewriter = rewriter;
        _history = history;
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

    // 注目文節の候補をインデックス指定で選択(候補ウィンドウのクリック/番号キー相当)。
    public bool SelectCandidate(int index)
    {
        if (CurrentState != State.Conversion)
        {
            return false;
        }
        Segment seg = _segments!.ConversionSegment(_focusedSegment);
        if (index < 0 || index >= seg.CandidatesSize)
        {
            return false;
        }
        _selected[_focusedSegment] = index;
        return true;
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

    // 確定文字列を返し、状態を初期化する。確定時は履歴予測へ学習する。
    public string Commit()
    {
        string result = GetPreedit();
        LearnHistory();
        Reset();
        return result;
    }

    // 変換中の各文節について (読み, 選択中の表記) を履歴学習する。
    private void LearnHistory()
    {
        if (_history == null || CurrentState != State.Conversion || _segments == null)
        {
            return;
        }
        for (int i = 0; i < _segments.ConversionSegmentsSize; i++)
        {
            Segment seg = _segments.ConversionSegment(i);
            if (seg.CandidatesSize > 0)
            {
                Candidate c = seg.Get(_selected[i]);
                _history.Learn(seg.Key, c.Value);
            }
        }
    }

    // 履歴予測(入力読みの前方一致)。候補ウィンドウ/サジェスト用。
    public List<Prediction.PredictionResult> PredictFromHistory(int maxResults = 10)
    {
        if (_history == null)
        {
            return new List<Prediction.PredictionResult>();
        }
        return _history.Predict(_composer.GetQueryForConversion(), maxResults);
    }

    // 履歴予測 + 辞書予測を統合(履歴を上位に、value 重複は低コスト採用、コスト昇順)。
    // C++ の predictor aggregator 相当の中核スライス。
    public List<Prediction.PredictionResult> PredictMerged(int maxResults = 10)
    {
        string query = _composer.GetQueryForConversion();
        var best = new Dictionary<string, Prediction.PredictionResult>();

        // 履歴予測は辞書より優先(コストを大きく下げて上位固定)。
        if (_history != null)
        {
            foreach (Prediction.PredictionResult r in _history.Predict(query, maxResults))
            {
                var boosted = new Prediction.PredictionResult
                {
                    Key = r.Key, Value = r.Value, Lid = r.Lid, Rid = r.Rid,
                    Wcost = r.Wcost, Cost = r.Cost - 10000,
                };
                if (!best.TryGetValue(r.Value, out var cur) || boosted.Cost < cur.Cost)
                {
                    best[r.Value] = boosted;
                }
            }
        }

        foreach (Prediction.PredictionResult r in _engine.Predict(query, maxResults))
        {
            if (!best.TryGetValue(r.Value, out var cur) || r.Cost < cur.Cost)
            {
                best[r.Value] = r;
            }
        }

        var ordered = new List<Prediction.PredictionResult>(best.Values);
        ordered.Sort((a, b) => a.Cost != b.Cost
            ? a.Cost.CompareTo(b.Cost)
            : string.CompareOrdinal(a.Value, b.Value));
        if (ordered.Count > maxResults)
        {
            ordered.RemoveRange(maxResults, ordered.Count - maxResults);
        }
        return ordered;
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
