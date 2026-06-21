namespace Mozc.Prediction;

// C++ src/prediction/suggestion_filter.cc 相当。サジェストに出すと不適切な語
// (suggestion_filter テキスト由来)を抑制する。本家は bloom filter だが、
// ここでは正規化済み集合での完全一致判定にする(誤検出ゼロ・決定的)。
// 集合データは DataGen で suggestion_filter.txt から構築して注入する。
public sealed class SuggestionFilter
{
    private readonly HashSet<string> _filtered;

    public SuggestionFilter(IEnumerable<string> filteredWords)
    {
        _filtered = new HashSet<string>();
        foreach (string w in filteredWords)
        {
            string n = Normalize(w);
            if (n.Length > 0)
            {
                _filtered.Add(n);
            }
        }
    }

    public static SuggestionFilter Empty { get; } = new(global::System.Array.Empty<string>());

    // value がフィルタ対象なら true(=サジェストから除外すべき)。
    public bool ShouldSuppress(string value) => _filtered.Contains(Normalize(value));

    // 予測候補列からフィルタ対象を取り除く(順序保持)。
    public List<PredictionResult> Apply(IReadOnlyList<PredictionResult> candidates)
    {
        var result = new List<PredictionResult>(candidates.Count);
        foreach (PredictionResult c in candidates)
        {
            if (!ShouldSuppress(c.Value))
            {
                result.Add(c);
            }
        }
        return result;
    }

    // C++ と同様に小文字化して比較(ASCII 範囲)。
    private static string Normalize(string s) => s.ToLowerInvariant();
}
