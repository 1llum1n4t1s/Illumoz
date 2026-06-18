using Mozc.Base;
using Mozc.Converter;
using Mozc.Dictionary;

namespace Mozc.Prediction;

// 予測結果(1 件)。C++ prediction::Result の中核フィールド。
public sealed class PredictionResult
{
    public string Key { get; init; } = string.Empty;   // 読み(辞書キー)
    public string Value { get; init; } = string.Empty; // 表記
    public ushort Lid { get; init; }
    public ushort Rid { get; init; }
    public int Wcost { get; init; }   // 辞書由来の語コスト
    public int Cost { get; set; }     // 順位付け後の最終コスト
}

// C++ src/prediction/dictionary_predictor.cc の unigram 予測スライス。
// LookupPredictive で読み前方一致を集め、GetLMCost + 長さボーナスで順位付けする。
// 履歴(bigram)・realtime・suffix・zero-query・aggressive 抑制は後続。
public sealed class DictionaryPredictor
{
    private readonly DictionaryBase _dictionary;
    private readonly Connector _connector;
    private readonly Segmenter _segmenter;
    private readonly SuggestionFilter _suggestionFilter;

    // 数の読み→数字の予測(任意)。設定時は Predict に数字候補を併合する。
    private readonly NumberDecoder? _numberDecoder;
    private readonly ushort _numberId;
    private readonly ushort _kanjiNumberId;

    private const int CostFactor = 500;            // C++ kCostFactor
    private const int DefaultPredictionLimit = 100;

    public DictionaryPredictor(DictionaryBase dictionary, Connector connector, Segmenter segmenter,
        SuggestionFilter? suggestionFilter = null,
        NumberDecoder? numberDecoder = null, ushort numberId = 0, ushort kanjiNumberId = 0)
    {
        _dictionary = dictionary;
        _connector = connector;
        _segmenter = segmenter;
        _suggestionFilter = suggestionFilter ?? SuggestionFilter.Empty;
        _numberDecoder = numberDecoder;
        _numberId = numberId;
        _kanjiNumberId = kanjiNumberId;
    }

    // C++ GetLMCost(unigram, history_rid=0): cost_with/without_context は等しくなるので
    // transition(0, lid) + wcost + suffixPenalty(rid)。
    private int GetLmCost(PredictionResult r, int historyRid)
    {
        int costWithContext = _connector.GetTransitionCost(historyRid, r.Lid);
        int costWithoutContext = _connector.GetTransitionCost(0, r.Lid);
        int lmCost = global::System.Math.Min(costWithContext, costWithoutContext) + r.Wcost;
        lmCost += _segmenter.GetSuffixPenalty(r.Rid);
        return lmCost;
    }

    // 読み query に対する予測候補(コスト昇順、value 重複排除)。
    public List<PredictionResult> Predict(string query, int maxResults = 10)
    {
        if (string.IsNullOrEmpty(query))
        {
            return new List<PredictionResult>();
        }

        var raw = new List<PredictionResult>();
        var callback = new InlineDictionaryCallback
        {
            TokenHandler = (key, expandedKey, token) =>
            {
                raw.Add(new PredictionResult
                {
                    Key = token.Key.Length != 0 ? token.Key : key,
                    Value = token.Value,
                    Lid = token.Lid,
                    Rid = token.Rid,
                    Wcost = token.Cost,
                });
                return raw.Count >= DefaultPredictionLimit
                    ? DictionaryCallback.ResultType.TraverseDone
                    : DictionaryCallback.ResultType.TraverseContinue;
            },
        };
        _dictionary.LookupPredictive(query, callback);

        // 数の読みなら数字候補(20, 1万 等)を併合する。
        if (_numberDecoder != null)
        {
            raw.AddRange(_numberDecoder.Aggregate(query, _numberId, _kanjiNumberId));
        }

        int queryLen = ScriptClassifier.CharsLen(query);
        foreach (PredictionResult r in raw)
        {
            int lmCost = GetLmCost(r, 0);
            int keyLen = ScriptClassifier.CharsLen(r.Key);
            int remain = global::System.Math.Max(0, keyLen - queryLen);
            // cost = lm_cost - 500 * log(1 + remain_length): 入力短縮ぶんを割り引く。
            r.Cost = lmCost - (int)(CostFactor * global::System.Math.Log(1.0 + remain));
        }

        // value 重複は最小コストを採用、コスト昇順で上位 maxResults。
        // サジェストフィルタ対象は除外する。
        var best = new Dictionary<string, PredictionResult>();
        foreach (PredictionResult r in raw)
        {
            if (_suggestionFilter.ShouldSuppress(r.Value))
            {
                continue;
            }
            if (!best.TryGetValue(r.Value, out PredictionResult? cur) || r.Cost < cur.Cost)
            {
                best[r.Value] = r;
            }
        }
        var ordered = new List<PredictionResult>(best.Values);
        ordered.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : string.CompareOrdinal(a.Value, b.Value));
        if (ordered.Count > maxResults)
        {
            ordered.RemoveRange(maxResults, ordered.Count - maxResults);
        }
        return ordered;
    }
}
