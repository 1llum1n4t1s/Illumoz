using System;
using System.Text;
using Mozc.Base;

namespace Mozc.Rewriter;

// C++ src/rewriter/language_aware_rewriter.cc の中核(IsRawQuery 判定)。
// ローマ字入力中に、打鍵そのもの(例 "google")を英単語候補として出すべきかを判定する。
public static class LanguageAwareRewriter
{
    // rawText: 打鍵そのもの(GetRawString)。composition: 表示プリエディット
    // (GetStringForPreedit)。predictionKey: 末尾英字を除いた予測キー
    // (GetQueryForPrediction)。hasKey/hasValue: 辞書照合。rank: 候補挿入位置の目安。
    public static bool IsRawQuery(
        string rawText, string composition, string predictionKey,
        Func<string, bool> hasKey, Func<string, bool> hasValue, out int rank)
    {
        rank = 0;

        // 3文字以下は誤検出回避のため対象外("cat" 等)。
        if (rawText.Length <= 3)
        {
            return false;
        }
        // 表示と打鍵が同じなら追加不要。
        if (composition == rawText)
        {
            return false;
        }
        // 表示の半角ASCII化が打鍵と同じなら追加不要。
        if (JapaneseUtil.FullWidthAsciiToHalfWidthAscii(composition) == rawText)
        {
            return false;
        }
        // 予測キーの途中に英字があれば(例 "えぁｍｐぇ")生クエリとみなす。
        if (ContainsAlphabet(predictionKey))
        {
            rank = 0;
            return true;
        }
        // 予測キーが辞書のキーなら(例 "はな")生クエリ扱いしない(保守的)。
        if (hasKey(predictionKey))
        {
            return false;
        }
        // 打鍵が辞書の値にあれば(例 "remove")生クエリとみなす。
        if (hasValue(rawText))
        {
            rank = 2;
            return true;
        }
        return false;
    }

    private static bool ContainsAlphabet(string s)
    {
        foreach (Rune r in s.EnumerateRunes())
        {
            if (ScriptClassifier.Classify(r.Value) == ScriptType.Alphabet)
            {
                return true;
            }
        }
        return false;
    }
}
