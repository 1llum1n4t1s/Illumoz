using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mozc.Prediction;

// C++ src/prediction/number_decoder.cc 相当。日本語の数の読み(「にじゅう」「ひゃく」
// 「いちまんにせん」等)をアラビア数字/漢数字混じり表記へデコードする。
// 例: にじゅう→20、せんにひゃく→1200、いちまんにせん→1万2000。
public sealed class NumberDecoder
{
    private enum EntryType
    {
        StopDecoding,
        Unit,
        SmallDigit,
        BigDigit,
        UnitAndBigDigit,
        UnitAndStopDecoding,
    }

    private readonly record struct Entry(
        EntryType Type, int Number, int Digit, string DigitStr,
        bool OutputBeforeDecode, int ConsumeRunesOfFirst);

    public readonly record struct DecodeResult(int ConsumedRunes, string Candidate, int DigitNum);

    private sealed class State
    {
        public int SmallDigitNum = -1;
        public StringBuilder CurrentNumStr = new();
        public int SmallDigit = -1;
        public int BigDigit = -1;
        public int ConsumedRunes;
        public List<string> ConsumedKeys = new();
        public int DigitNum;
        public string Key = string.Empty;

        public bool IsValid() => !(SmallDigitNum == -1 && SmallDigit == -1 && BigDigit == -1);

        public DecodeResult? Result()
        {
            if (!IsValid())
            {
                return null;
            }
            int small = global::System.Math.Max(SmallDigitNum, 0);
            if (small > 0)
            {
                return new DecodeResult(ConsumedRunes,
                    CurrentNumStr.ToString() + small.ToString(CultureInfo.InvariantCulture), DigitNum);
            }
            if (CurrentNumStr.Length > 0)
            {
                return new DecodeResult(ConsumedRunes, CurrentNumStr.ToString(), DigitNum);
            }
            if (small == 0)
            {
                return new DecodeResult(ConsumedRunes, "0", 1);
            }
            return null;
        }
    }

    private static readonly Dictionary<string, Entry> Entries = BuildEntries();

    // 最長一致用に存在するキー長の集合(降順)。
    private static readonly int[] KeyLengths = BuildKeyLengths();

    // key(かな読み)を主デコード結果の文字列にする。デコードできなければ null。
    public string? DecodeToString(string key)
    {
        var state = new State { Key = key };
        var results = new List<DecodeResult>();
        DecodeAux(key, state, results);
        MaybeAppendResult(state, results);
        return results.Count > 0 ? results[^1].Candidate : null;
    }

    // 全デコード結果(部分消費を含む)を返す。
    public IReadOnlyList<DecodeResult> Decode(string key)
    {
        var state = new State { Key = key };
        var results = new List<DecodeResult>();
        DecodeAux(key, state, results);
        MaybeAppendResult(state, results);
        return results;
    }

    private void DecodeAux(string key, State state, List<DecodeResult> results)
    {
        if (key.Length == 0)
        {
            return;
        }
        if (!LongestMatch(key, out Entry e, out int matchLen))
        {
            return;
        }
        string k = key.Substring(0, matchLen);
        switch (e.Type)
        {
            case EntryType.StopDecoding:
                return;
            case EntryType.Unit:
                if (!HandleUnit(k, e, state, results)) { return; }
                state.ConsumedRunes += RuneLen(k);
                break;
            case EntryType.SmallDigit:
                if (!HandleSmallDigit(k, e, state, results)) { return; }
                state.ConsumedRunes += RuneLen(k);
                break;
            case EntryType.BigDigit:
                if (!HandleBigDigit(k, e, state, results)) { return; }
                state.ConsumedRunes += RuneLen(k);
                break;
            case EntryType.UnitAndBigDigit:
            {
                string unitKey = Runes(key, 0, e.ConsumeRunesOfFirst);
                if (!HandleUnit(unitKey, e, state, results)) { return; }
                state.ConsumedRunes += e.ConsumeRunesOfFirst;
                int restRunes = RuneLen(k) - e.ConsumeRunesOfFirst;
                string digitKey = Runes(key, e.ConsumeRunesOfFirst, restRunes);
                if (!HandleBigDigit(digitKey, e, state, results)) { return; }
                state.ConsumedRunes += restRunes;
                break;
            }
            case EntryType.UnitAndStopDecoding:
            {
                string unitKey = Runes(key, 0, e.ConsumeRunesOfFirst);
                if (!HandleUnit(unitKey, e, state, results)) { return; }
                state.ConsumedRunes += e.ConsumeRunesOfFirst;
                return;
            }
        }
        DecodeAux(Runes(key, matchLen, RuneLen(key) - matchLen), state, results);
    }

    private bool HandleUnit(string key, Entry entry, State state, List<DecodeResult> results)
    {
        results.Clear();
        if (state.IsValid() && entry.Number == 0)
        {
            return false; // 0 は従属数としてのみ。
        }
        if (state.SmallDigitNum == 0
            || (state.SmallDigitNum != -1 && state.SmallDigitNum % 10 != 0))
        {
            return false; // 既に Unit を消費済み(いちさん, ぜろご)。
        }
        if (entry.OutputBeforeDecode)
        {
            MaybeAppendResult(state, results);
        }
        if (state.SmallDigitNum == -1)
        {
            state.SmallDigitNum = entry.Number;
        }
        else
        {
            state.SmallDigitNum += entry.Number;
        }
        state.ConsumedKeys.Add(key);
        state.DigitNum = global::System.Math.Max(state.DigitNum, 1);
        return true;
    }

    private bool HandleSmallDigit(string key, Entry entry, State state, List<DecodeResult> results)
    {
        results.Clear();
        if (state.SmallDigit > 1 && entry.Digit >= state.SmallDigit)
        {
            return false; // じゅうせん
        }
        if (state.SmallDigitNum == 0)
        {
            return false; // ぜろじゅう
        }
        if (entry.OutputBeforeDecode)
        {
            MaybeAppendResult(state, results);
        }
        if (state.SmallDigitNum == -1)
        {
            state.SmallDigitNum = entry.Number;
        }
        else
        {
            int unit = global::System.Math.Max(1, state.SmallDigitNum % 10);
            int baseNum = (state.SmallDigitNum / 10) * 10;
            state.SmallDigitNum = baseNum + unit * entry.Number;
        }
        state.SmallDigit = entry.Digit;
        state.ConsumedKeys.Add(key);
        state.DigitNum = global::System.Math.Max(state.DigitNum, entry.Digit);
        return true;
    }

    private bool HandleBigDigit(string key, Entry entry, State state, List<DecodeResult> results)
    {
        results.Clear();
        if (state.BigDigit > 0 && entry.Digit >= state.BigDigit)
        {
            return false; // おくまん
        }
        if (state.SmallDigitNum == -1 || state.SmallDigitNum == 0)
        {
            return false; // "まん" 単独を 10000 にしない。
        }
        if (entry.OutputBeforeDecode)
        {
            MaybeAppendResult(state, results);
        }
        state.CurrentNumStr.Append(state.SmallDigitNum.ToString(CultureInfo.InvariantCulture))
            .Append(entry.DigitStr);
        state.DigitNum = global::System.Math.Max(state.DigitNum,
            entry.Digit + state.SmallDigitNum.ToString(CultureInfo.InvariantCulture).Length - 1);
        state.SmallDigitNum = -1;
        state.SmallDigit = -1;
        state.BigDigit = entry.Digit;
        state.ConsumedKeys.Add(key);
        return true;
    }

    private static void MaybeAppendResult(State state, List<DecodeResult> results)
    {
        DecodeResult? result = state.Result();
        if (result == null)
        {
            return;
        }
        var keys = new List<string>(state.ConsumedKeys);
        if (state.ConsumedRunes < RuneLen(state.Key))
        {
            keys.Add(Runes(state.Key, state.ConsumedRunes, RuneLen(state.Key) - state.ConsumedRunes));
        }
        for (int i = 0; i < keys.Count; i++)
        {
            string k = keys[i];
            if ((k == "よ" || k == "く") && i + 1 < keys.Count)
            {
                return; // 末尾以外は無効。
            }
            if (k == "し" && !(i + 1 == keys.Count || keys[i + 1] == "じゅう"))
            {
                return; // し:4 / じゅうし / しじゅう のみ有効。
            }
        }
        results.Add(result.Value);
    }

    // 残り key の先頭に対する最長一致エントリ。
    private static bool LongestMatch(string key, out Entry entry, out int matchLen)
    {
        foreach (int len in KeyLengths)
        {
            if (len > key.Length)
            {
                continue;
            }
            string head = key.Substring(0, len);
            if (Entries.TryGetValue(head, out entry))
            {
                matchLen = len;
                return true;
            }
        }
        entry = default;
        matchLen = 0;
        return false;
    }

    // --- ヘルパ(Rune 単位アクセス) ---
    private static int RuneLen(string s)
    {
        int n = 0;
        foreach (System.Text.Rune _ in s.EnumerateRunes())
        {
            n++;
        }
        return n;
    }

    private static string Runes(string s, int start, int count)
    {
        var sb = new StringBuilder();
        int idx = 0;
        foreach (System.Text.Rune r in s.EnumerateRunes())
        {
            if (idx >= start && idx < start + count)
            {
                sb.Append(r.ToString());
            }
            idx++;
        }
        return sb.ToString();
    }

    private static int[] BuildKeyLengths()
    {
        var set = new SortedSet<int>();
        foreach (string k in Entries.Keys)
        {
            set.Add(k.Length);
        }
        var arr = new List<int>(set);
        arr.Reverse(); // 長い順。
        return arr.ToArray();
    }

    private static Dictionary<string, Entry> BuildEntries()
    {
        var e = new Dictionary<string, Entry>();
        void Unit(string k, int n) => e[k] = new Entry(EntryType.Unit, n, 0, "", false, 0);
        void Small(string k, int n, int d, bool obd = false)
            => e[k] = new Entry(EntryType.SmallDigit, n, d, "", obd, 0);
        void Big(string k, int d, string ds, bool obd = false)
            => e[k] = new Entry(EntryType.BigDigit, -1, d, ds, obd, 0);

        Unit("ぜろ", 0); Unit("いち", 1); Unit("いっ", 1); Unit("に", 2); Unit("さん", 3);
        Unit("し", 4); Unit("よん", 4); Unit("よ", 4); Unit("ご", 5); Unit("ろく", 6);
        Unit("ろっ", 6); Unit("なな", 7); Unit("しち", 7); Unit("はち", 8); Unit("はっ", 8);
        Unit("きゅう", 9); Unit("きゅー", 9); Unit("く", 9);

        Small("じゅう", 10, 2, true); Small("じゅー", 10, 2, true); Small("じゅっ", 10, 2);
        Small("ひゃく", 100, 3); Small("ひゃっ", 100, 3); Small("びゃく", 100, 3);
        Small("びゃっ", 100, 3); Small("ぴゃく", 100, 3); Small("ぴゃっ", 100, 3);
        Small("せん", 1000, 4, true); Small("ぜん", 1000, 4, true);

        Big("まん", 5, "万"); Big("おく", 9, "億"); Big("おっ", 9, "億");
        Big("ちょう", 13, "兆", true); Big("けい", 17, "京", true); Big("がい", 21, "垓");

        // 特殊ケース。
        e["にちょう"] = new Entry(EntryType.UnitAndBigDigit, 2, 13, "兆", true, 1);   // に+ちょう
        e["にちょうめ"] = new Entry(EntryType.UnitAndStopDecoding, 2, -1, "", false, 1);
        e["にちゃん"] = new Entry(EntryType.UnitAndStopDecoding, 2, -1, "", false, 1);
        e["さんちーむ"] = new Entry(EntryType.UnitAndStopDecoding, 3, -1, "", true, 2);

        // 数の読みと衝突する接尾語(STOP_DECODING)。
        foreach (string s in new[]
        {
            "にぎり", "にち", "にん",
            "しーしー", "しーと", "しーべると", "しあい", "しき", "しつ", "しな",
            "しゃ", "しゅ", "しょう",
        })
        {
            e[s] = new Entry(EntryType.StopDecoding, 0, 0, "", false, 0);
        }
        return e;
    }
}
