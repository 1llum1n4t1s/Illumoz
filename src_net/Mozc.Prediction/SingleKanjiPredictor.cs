using System.Collections.Generic;
using System.Text;

namespace Mozc.Prediction;

// C++ src/prediction/single_kanji_decoder.cc 相当。読みに対応する単漢字候補を
// 予測結果として返す。読み→単漢字リスト(表の並び順=優先順)を保持し、
// 長いキーを優先しつつ後ろを削った部分キーでも補完する(任意)。
public sealed class SingleKanjiPredictor
{
    private readonly IReadOnlyDictionary<string, string[]> _table; // 読み→単漢字[]
    private readonly ushort _generalSymbolId;

    private const int MinSingleKanjiSize = 5;
    private const int ShorterKeyOffset = 3450; // 500 * log(1000)

    public SingleKanjiPredictor(IReadOnlyDictionary<string, string[]> table, ushort generalSymbolId = 0)
    {
        _table = table;
        _generalSymbolId = generalSymbolId;
    }

    // requestKey の単漢字予測。allowPartial=false なら完全一致キーのみ。
    public List<PredictionResult> Decode(string requestKey, bool allowPartial = false)
    {
        var results = new List<PredictionResult>();
        if (string.IsNullOrEmpty(requestKey))
        {
            return results;
        }

        int offset = 0;
        for (string key = requestKey; key.Length > 0; key = StripLastChar(key))
        {
            if (!allowPartial && key != requestKey)
            {
                break; // 部分結果を含めない。
            }
            if (!_table.TryGetValue(key, out string[]? kanjiList) || kanjiList.Length == 0)
            {
                continue;
            }
            AppendResults(key, kanjiList, offset, results);
            // 短いキーの候補は長いキーより下位に。
            offset += ShorterKeyOffset;
            if (results.Count > MinSingleKanjiSize)
            {
                break;
            }
        }
        return results;
    }

    private void AppendResults(string kanjiKey, string[] kanjiList, int offset, List<PredictionResult> results)
    {
        foreach (string kanji in kanjiList)
        {
            results.Add(new PredictionResult
            {
                Key = kanjiKey,
                Value = kanji,
                Lid = _generalSymbolId,
                Rid = _generalSymbolId,
                Wcost = offset + results.Count, // リスト順を保つ wcost。
            });
        }
    }

    // 末尾の1コードポイントを削る。1文字以下なら空。
    private static string StripLastChar(string key)
    {
        var runes = new List<System.Text.Rune>();
        foreach (System.Text.Rune r in key.EnumerateRunes())
        {
            runes.Add(r);
        }
        if (runes.Count <= 1)
        {
            return string.Empty;
        }
        var sb = new StringBuilder();
        for (int i = 0; i < runes.Count - 1; i++)
        {
            sb.Append(runes[i].ToString());
        }
        return sb.ToString();
    }
}
