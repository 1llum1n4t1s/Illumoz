namespace Mozc.Prediction;

// C++ src/prediction/user_history_predictor.cc の中核スライス。確定された
// (読み, 表記) を学習し、入力読みの前方一致で履歴予測を返す。頻度と最終アクセス
// 時刻(recency)でランク付けし、容量超過時は LRU で破棄する。
// 完全自己完結(外部データ不要)。時刻はテスト容易性のため注入する。
public sealed class UserHistoryPredictor
{
    // 1 履歴エントリ。Key=読み, Value=確定表記, Frequency=確定回数, LastAccess=最終時刻。
    public sealed class Entry
    {
        public string Key = string.Empty;
        public string Value = string.Empty;
        public int Frequency;
        public long LastAccess;
    }

    private const int DefaultCapacity = 3000; // C++ kMaxEntrySize 相当(縮小)。
    private readonly int _capacity;
    private readonly Func<long> _now;
    // (Key,Value) 一意。挿入順は LastAccess/Frequency で管理。
    private readonly Dictionary<(string, string), Entry> _entries = new();

    public UserHistoryPredictor(int capacity = DefaultCapacity, Func<long>? clock = null)
    {
        _capacity = capacity;
        _now = clock ?? (() => global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public int Count => _entries.Count;

    // 確定時に学習する(C++ Finish 相当)。空・同一は無視。
    public void Learn(string key, string value)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
        {
            return;
        }
        long t = _now();
        if (_entries.TryGetValue((key, value), out Entry? e))
        {
            e.Frequency++;
            e.LastAccess = t;
        }
        else
        {
            _entries[(key, value)] = new Entry
            {
                Key = key,
                Value = value,
                Frequency = 1,
                LastAccess = t,
            };
            EvictIfNeeded();
        }
    }

    // 容量超過なら LastAccess 最古(同点は Frequency 最小)を破棄。
    private void EvictIfNeeded()
    {
        while (_entries.Count > _capacity)
        {
            (string, string) victim = default;
            long oldest = long.MaxValue;
            int minFreq = int.MaxValue;
            foreach (KeyValuePair<(string, string), Entry> kv in _entries)
            {
                Entry en = kv.Value;
                if (en.LastAccess < oldest || (en.LastAccess == oldest && en.Frequency < minFreq))
                {
                    oldest = en.LastAccess;
                    minFreq = en.Frequency;
                    victim = kv.Key;
                }
            }
            _entries.Remove(victim);
        }
    }

    // 読み query(前方一致)に対する履歴予測。Frequency↓→recency↓→Value で安定整列。
    public List<PredictionResult> Predict(string query, int maxResults = 10)
    {
        var results = new List<PredictionResult>();
        if (string.IsNullOrEmpty(query))
        {
            return results;
        }

        var hits = new List<Entry>();
        foreach (Entry e in _entries.Values)
        {
            if (e.Key.StartsWith(query, global::System.StringComparison.Ordinal))
            {
                hits.Add(e);
            }
        }
        hits.Sort((a, b) =>
        {
            int c = b.Frequency.CompareTo(a.Frequency);
            if (c != 0) { return c; }
            c = b.LastAccess.CompareTo(a.LastAccess);
            if (c != 0) { return c; }
            return string.CompareOrdinal(a.Value, b.Value);
        });

        int n = global::System.Math.Min(maxResults, hits.Count);
        for (int i = 0; i < n; i++)
        {
            Entry e = hits[i];
            results.Add(new PredictionResult
            {
                Key = e.Key,
                Value = e.Value,
                // 頻度が高いほど低コスト(辞書予測より上位に出やすい)。
                Cost = HistoryCost(e),
            });
        }
        return results;
    }

    // 履歴コスト: 基準から頻度の対数で減算。C++ は別式だが順位の単調性を満たす近似。
    private static int HistoryCost(Entry e)
    {
        double bonus = 500.0 * global::System.Math.Log(1 + e.Frequency);
        return (int)global::System.Math.Max(1, 1000 - bonus);
    }

    // 履歴の全消去(C++ ClearAllHistory 相当)。
    public void Clear() => _entries.Clear();

    // 特定エントリの消去(プライバシー: C++ ClearHistoryEntry 相当)。
    public bool Remove(string key, string value) => _entries.Remove((key, value));

    // 永続化用スナップショット(保存/復元は呼び出し側が担う)。
    public IReadOnlyCollection<Entry> Snapshot() => _entries.Values;

    public void Restore(IEnumerable<Entry> entries)
    {
        foreach (Entry e in entries)
        {
            if (!string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Value))
            {
                _entries[(e.Key, e.Value)] = e;
            }
        }
        EvictIfNeeded();
    }
}
