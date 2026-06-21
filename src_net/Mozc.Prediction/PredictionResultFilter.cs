using System.Collections.Generic;
using System.Text;
using Mozc.Base;

namespace Mozc.Prediction;

// C++ src/prediction/result_filter.cc の冗長判定/重複排除の中核スライス。
public static class PredictionResultFilter
{
    // target が reference に対して冗長か(C++ MaybeRedundant)。
    // 同一値=冗長。キーが同じで値が違うなら非冗長。target が reference の前方一致で
    // 追記部分が通常スクリプト(かな/漢字/英数)なら冗長、絵文字/不明なら非冗長。
    public static bool MaybeRedundant(
        string referenceKey, string referenceValue, string targetKey, string targetValue)
    {
        if (referenceValue == targetValue)
        {
            return true;
        }
        if (referenceKey == targetKey)
        {
            return false; // 値が違うので非冗長。
        }
        if (!targetValue.StartsWith(referenceValue, global::System.StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = targetValue.Substring(referenceValue.Length);
        // 追記部分が「通常スクリプト」なら冗長。絵文字/記号/不明(=Other)は非冗長。
        return SuffixIsOrdinaryScript(suffix);
    }

    private static bool SuffixIsOrdinaryScript(string suffix)
    {
        if (suffix.Length == 0)
        {
            return true;
        }
        // 全コードポイントが Other(絵文字/記号/不明)なら非冗長。
        foreach (System.Text.Rune r in suffix.EnumerateRunes())
        {
            if (ScriptClassifier.Classify(r.Value) != ScriptType.Other)
            {
                return true; // ひとつでも通常スクリプトがあれば冗長扱い。
            }
        }
        return false;
    }

    // 値の重複を除き、先に採用した候補に対して冗長な候補も落とす。
    // 入力順を保つ(コスト順で渡される前提)。
    public static List<PredictionResult> Dedup(IReadOnlyList<PredictionResult> results)
    {
        var kept = new List<PredictionResult>();
        var seenValues = new HashSet<string>();
        foreach (PredictionResult r in results)
        {
            if (!seenValues.Add(r.Value))
            {
                continue; // 同一値は除外。
            }
            bool redundant = false;
            foreach (PredictionResult k in kept)
            {
                if (MaybeRedundant(k.Key, k.Value, r.Key, r.Value))
                {
                    redundant = true;
                    break;
                }
            }
            if (!redundant)
            {
                kept.Add(r);
            }
        }
        return kept;
    }
}
