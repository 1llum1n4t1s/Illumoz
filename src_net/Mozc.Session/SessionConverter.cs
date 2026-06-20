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
    private readonly Dictionary.UserDictionaryStorage? _userDict;
    private Composer.Composer _composer;

    private Segments? _segments;
    private int _focusedSegment;
    private int[] _selected = global::System.Array.Empty<int>();

    public SessionConverter(MozcEngine engine, IRewriter? rewriter = null,
        Prediction.UserHistoryPredictor? history = null,
        Dictionary.UserDictionaryStorage? userDict = null)
    {
        _engine = engine;
        _rewriter = rewriter;
        _history = history;
        _userDict = userDict;
        _composer = engine.CreateComposer();
    }

    public State CurrentState { get; private set; } = State.Composition;
    public int FocusedSegment => _focusedSegment;

    // idle(入力中でも変換中でもない)なら engine の最新ローマ字表で composer を作り直す。
    // custom_roman_table 等の設定変更を、既存セッションの次の入力から反映させる。
    public void RefreshComposerIfIdle()
    {
        if (CurrentState == State.Composition && _composer.GetStringForPreedit().Length == 0)
        {
            _composer = _engine.CreateComposer();
        }
    }

    // 入力(ローマ字 1 打鍵)。変換中なら一旦確定してから新規入力。
    // F6-F10 等で確定する表記変換(T13N)結果。null 以外なら preedit/commit に優先。
    private string? _t13n;

    public void InsertCharacter(string key)
    {
        if (CurrentState == State.Conversion)
        {
            Commit();
        }
        _t13n = null;
        _composer.InsertCharacter(key);
    }

    // composer の表記変換(ひらがな/全角カナ/半角カナ/全角英数/半角英数)を選んで
    // preedit に反映する(C++ session の ConvertToHiragana 等に相当)。
    public void ConvertToTransliteration(global::System.Func<Composer.Composer, string> picker)
    {
        // 変換中なら一旦かな入力状態に戻す扱い(候補ではなく読みの表記変換)。
        _t13n = picker(_composer);
    }

    // 未確定の preedit(変換中は選択中候補列、未変換時はかな preedit)。
    public string GetPreedit()
    {
        if (_t13n != null)
        {
            return _t13n;
        }
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

    // 各変換文節の読みに完全一致するユーザー辞書語を先頭候補へ挿入する。
    private void InsertUserDictionaryCandidates(Segments segments)
    {
        if (_userDict == null)
        {
            return;
        }
        for (int i = 0; i < segments.ConversionSegmentsSize; i++)
        {
            Segment seg = segments.ConversionSegment(i);
            var matches = _userDict.LookupExact(seg.Key);
            if (matches.Count == 0)
            {
                continue;
            }
            var existing = new HashSet<string>();
            for (int j = 0; j < seg.CandidatesSize; j++)
            {
                existing.Add(seg.Get(j).Value);
            }
            // システムが基底候補を出せない新規読みでも、ユーザー辞書語を候補として合成する
            // (候補 0 件のままだと Convert() が変換を破棄して登録語が一生出てこない)。
            int baseCost = seg.CandidatesSize > 0 ? seg.Get(0).Cost : 0;
            int insertAt = 0;
            foreach (var m in matches)
            {
                if (existing.Add(m.Word))
                {
                    seg.InsertCandidate(insertAt++, new Candidate
                    {
                        Key = seg.Key,
                        Value = m.Word,
                        ContentKey = seg.Key,
                        ContentValue = m.Word,
                        Description = "ユーザー辞書",
                        Cost = baseCost - 1000,
                    });
                }
            }
        }
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
        InsertUserDictionaryCandidates(_segments);
        int n = _segments.ConversionSegmentsSize;
        // Viterbi がパスを構築できないと文節は 1 つでも候補 0 件のことがある。候補が
        // 1 件も無いまま Conversion に入ると GetPreedit が候補 0 を引いて壊れるため弾く。
        if (n == 0 || _segments.ConversionSegment(0).CandidatesSize == 0)
        {
            _segments = null;
            return false;
        }
        _selected = new int[n];
        _focusedSegment = 0;
        _t13n = null; // 変換に入ったら F6-F10 の表記変換オーバーライドは無効化する。
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
        _t13n = null; // 候補移動したら表記変換オーバーライドより選択候補を優先する。
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
        _t13n = null; // 候補を選んだら表記変換オーバーライドより選択候補を優先する。
        _selected[_focusedSegment] = index;
        return true;
    }

    // ショートカット文字(例 "123456789" の '3')で候補を選択する。
    // shortcuts に無い文字や範囲外は false。
    public bool SelectByShortcut(char shortcut, string shortcuts)
    {
        int idx = shortcuts.IndexOf(shortcut);
        return idx >= 0 && SelectCandidate(idx);
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
    // CommandRewriter 由来のコマンド候補(設定トグル等)は、ラベル文字列を
    // 確定テキストとして挿入せず空文字を返す(誤ってラベルが入力される不具合の修正)。
    // 直近の Commit で確定したコマンド候補のコマンド(無ければ DefaultCommand)。
    // EngineServer が incognito/presentation トグル等を実行するために読む。
    public Candidate.CommandType LastCommand { get; private set; } = Candidate.CommandType.DefaultCommand;

    // LastCommand を取り出して既定へ戻す(EngineServer が 1 回だけ実行するため)。
    public Candidate.CommandType TakeLastCommand()
    {
        Candidate.CommandType c = LastCommand;
        LastCommand = Candidate.CommandType.DefaultCommand;
        return c;
    }

    public string Commit()
    {
        LastCommand = GetSelectedCommand(); // Reset 前に確定候補のコマンドを退避。
        string result = SelectedCandidateIsCommand() ? string.Empty : GetPreedit();
        LearnHistory();
        Reset();
        return result;
    }

    // 選択中候補がコマンド候補ならそのコマンドを返す(複数文節なら最初の 1 つ)。
    private Candidate.CommandType GetSelectedCommand()
    {
        if (CurrentState != State.Conversion || _segments == null)
        {
            return Candidate.CommandType.DefaultCommand;
        }
        for (int i = 0; i < _segments.ConversionSegmentsSize; i++)
        {
            Segment seg = _segments.ConversionSegment(i);
            if (seg.CandidatesSize > 0)
            {
                Candidate c = seg.Get(_selected[i]);
                if ((c.Attributes & Candidate.Attribute.CommandCandidate) != 0)
                {
                    return c.Command;
                }
            }
        }
        return Candidate.CommandType.DefaultCommand;
    }

    // 注目文節の選択候補がコマンド候補(CommandCandidate 属性)か。
    private bool SelectedCandidateIsCommand()
    {
        if (CurrentState != State.Conversion || _segments == null)
        {
            return false;
        }
        for (int i = 0; i < _segments.ConversionSegmentsSize; i++)
        {
            Segment seg = _segments.ConversionSegment(i);
            if (seg.CandidatesSize > 0 &&
                (seg.Get(_selected[i]).Attributes & Candidate.Attribute.CommandCandidate) != 0)
            {
                return true;
            }
        }
        return false;
    }

    // 変換中の各文節について (読み, 選択中の表記) を履歴学習する。
    private void LearnHistory()
    {
        // F6-F10 等の表記変換(T13N)で確定する場合、確定テキストは _t13n(変換後カナ/英数)で
        // あって _segments の選択候補ではない。古い辞書候補を学習すると「ユーザーが確定して
        // いないテキスト」で履歴が訓練されるため、実際に確定する T13N 値を読み(現在の composer
        // クエリ)に紐づけて学習する。状態ガードより前に置くことで Composition 中の T13N 確定も拾う。
        // (単文節 T13N は C++ と表示・学習とも一致。多文節 Conversion 中 T13N の per-segment 学習は対象外)
        if (_t13n != null)
        {
            _history?.Learn(_composer.GetQueryForConversion(), _t13n);
            return;
        }
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
                // NoLearning 属性(ダイス/おみくじ/コマンド候補等)は学習しない。
                if ((c.Attributes & Candidate.Attribute.NoLearning) != 0)
                {
                    continue;
                }
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

    // 入力前のゼロクエリ予測(履歴の直近)。履歴が無ければ空。
    public List<Prediction.PredictionResult> PredictZeroQuery(int maxResults = 5)
        => _history?.PredictZeroQuery(maxResults) ?? new List<Prediction.PredictionResult>();

    // 履歴予測 + 辞書予測を統合(履歴を上位に、value 重複は低コスト採用、コスト昇順)。
    // C++ の predictor aggregator 相当の中核スライス。
    public List<Prediction.PredictionResult> PredictMerged(
        int maxResults = 10, bool includeHistory = true, bool includeDictionary = true)
    {
        string query = _composer.GetQueryForConversion();
        var best = new Dictionary<string, Prediction.PredictionResult>();

        // ユーザー辞書(前方一致)を最優先(コスト最小で上位固定)。辞書サジェスト無効時は除外。
        if (_userDict != null && includeDictionary)
        {
            foreach (var e in _userDict.LookupPredictive(query))
            {
                var r = new Prediction.PredictionResult { Key = e.Reading, Value = e.Word, Cost = -20000 };
                if (!best.TryGetValue(e.Word, out var cur) || r.Cost < cur.Cost)
                {
                    best[e.Word] = r;
                }
            }
        }

        // 履歴予測は辞書より優先(コストを大きく下げて上位固定)。シークレット/履歴無効時は除外。
        if (_history != null && includeHistory)
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

        // 辞書/リアルタイム予測(engine)。辞書サジェスト無効時は除外。
        if (includeDictionary)
        {
            foreach (Prediction.PredictionResult r in _engine.Predict(query, maxResults))
            {
                if (!best.TryGetValue(r.Value, out var cur) || r.Cost < cur.Cost)
                {
                    best[r.Value] = r;
                }
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

    // 入力中(composition)にサジェスト候補を直接確定する。index 無効なら null。
    // 確定値を履歴学習しリセットする(C++ の suggestion commit 相当)。
    public string? CommitSuggestion(int index, bool includeHistory = true, bool includeDictionary = true)
    {
        if (CurrentState != State.Composition)
        {
            return null;
        }
        // 表示時と同じソース可否で再計算する。履歴/辞書の可否が表示と食い違うと、
        // 表示されていない候補(無効化したはずの履歴等)を確定してしまうため(プライバシー漏れ)。
        List<Prediction.PredictionResult> preds = PredictMerged(
            includeHistory: includeHistory, includeDictionary: includeDictionary);
        if (index < 0 || index >= preds.Count)
        {
            return null;
        }
        Prediction.PredictionResult p = preds[index];
        _history?.Learn(p.Key.Length > 0 ? p.Key : _composer.GetQueryForConversion(), p.Value);
        Reset();
        return p.Value;
    }

    // 変換を取り消して入力状態へ戻る(かなは保持)。
    public void Cancel()
    {
        // C++ EngineConverter::Cancel→ResetState 相当: キャンセル時は candidate_list_ を
        // Clear するので、表記変換(F6-F10)オーバーライドもここで破棄して composer のかなへ戻す。
        _segments = null;
        _t13n = null;
        CurrentState = State.Composition;
    }

    public void Reset()
    {
        _composer = _engine.CreateComposer();
        _segments = null;
        _selected = global::System.Array.Empty<int>();
        _focusedSegment = 0;
        CurrentState = State.Composition;
        _t13n = null;
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

    // 注目文節で選択中の候補インデックス(候補ウィンドウのハイライト用)。
    // 変換中でなければ -1(サジェスト等の「未注目」を表す)。
    public int FocusedCandidateIndex
        => CurrentState == State.Conversion ? _selected[_focusedSegment] : -1;

    // 注目文節の先頭が preedit 上で始まる文字位置(候補ウィンドウのアンカー = position)。
    // 先行文節の選択候補表記の文字数(書記素単位)を合算する。変換中でなければ 0。
    public int FocusedPosition
    {
        get
        {
            if (CurrentState != State.Conversion || _segments == null)
            {
                return 0;
            }
            int pos = 0;
            for (int i = 0; i < _focusedSegment && i < _segments.ConversionSegmentsSize; i++)
            {
                string v = _segments.ConversionSegment(i).Get(_selected[i]).Value;
                // エンジン全体と同じ書記素定義(C++ Util::SplitStringToUtf8Graphemes 相当)で
                // 数える。StringInfo だと絵文字/ZWJ の数え方がずれてアンカー位置が食い違う。
                pos += Mozc.Base.GraphemeSplitter.Split(v).Count;
            }
            return pos;
        }
    }

    // 注目文節の候補の説明(記号/全角/単漢字 等。候補と同数、無ければ空文字)。
    public IReadOnlyList<string> GetCandidateDescriptions()
    {
        if (CurrentState != State.Conversion)
        {
            return global::System.Array.Empty<string>();
        }
        Segment seg = _segments!.ConversionSegment(_focusedSegment);
        var list = new List<string>(seg.CandidatesSize);
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            list.Add(seg.Get(i).Description);
        }
        return list;
    }
}
