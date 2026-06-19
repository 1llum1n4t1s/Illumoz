using System.Collections.Generic;
using System.Text;
using Mozc.Base;
using Mozc.Dictionary;

namespace Mozc.Converter;

// C++ src/converter/history_reconstructor.cc 相当。
// 直前の確定済みテキスト(preceding_text)末尾の数字/英字トークンを
// HISTORY セグメントとして復元し、変換の文脈(連接)に使えるようにする。
public sealed class HistoryReconstructor
{
    private readonly PosMatcher _posMatcher;

    public HistoryReconstructor(PosMatcher posMatcher) => _posMatcher = posMatcher;

    // preceding_text から HISTORY セグメントを segments 先頭側へ追加する。
    // 末尾トークンが数字/英字でなければ false(何もしない)。
    public bool ReconstructHistory(string precedingText, Segments segments)
    {
        if (!GetLastConnectivePart(precedingText, out string key, out string value, out ushort id))
        {
            return false;
        }

        Segment segment = segments.AddSegment();
        segment.SetKey(key);
        segment.Type = Segment.SegmentType.History;
        Candidate c = segment.AddCandidate();
        c.Rid = id;
        c.Lid = id;
        c.ContentKey = key;
        c.Key = key;
        c.ContentValue = value;
        c.Value = value;
        c.Attributes = Candidate.Attribute.NoLearning;
        return true;
    }

    // 末尾の連接対象トークン(数字/英字)を取り出す。key は半角化した読み。
    public bool GetLastConnectivePart(string precedingText, out string key, out string value, out ushort id)
    {
        key = string.Empty;
        value = string.Empty;
        id = _posMatcher.GetGeneralNounId();

        if (!ExtractLastToken(precedingText, out string lastToken, out ScriptType lastType))
        {
            return false;
        }

        switch (lastType)
        {
            case ScriptType.Numeric:
                key = JapaneseUtil.FullWidthAsciiToHalfWidthAscii(lastToken);
                value = lastToken;
                id = _posMatcher.GetNumberId();
                return true;
            case ScriptType.Alphabet:
                key = JapaneseUtil.FullWidthAsciiToHalfWidthAscii(lastToken);
                value = lastToken;
                id = _posMatcher.GetUniqueNounId();
                return true;
            default:
                return false;
        }
    }

    // 末尾の同一文字種ランを取り出す(末尾の空白1つは許容)。C++ ExtractLastTokenWithScriptType。
    private static bool ExtractLastToken(string text, out string lastToken, out ScriptType lastScriptType)
    {
        lastToken = string.Empty;
        lastScriptType = ScriptType.Unknown;

        int[] cps = ToCodepoints(text);
        int pos = cps.Length - 1;
        if (pos < 0)
        {
            return false;
        }

        // 末尾の空白1つは許容(2つ続くなら不可)。
        if (cps[pos] == ' ')
        {
            pos--;
            if (pos < 0 || cps[pos] == ' ')
            {
                return false;
            }
        }

        ScriptType found = ScriptClassifier.Classify(cps[pos]);
        var reverse = new List<int>();
        for (; pos >= 0; pos--)
        {
            int c = cps[pos];
            if (c == ' ' || ScriptClassifier.Classify(c) != found)
            {
                break;
            }
            reverse.Add(c);
        }

        lastScriptType = found;
        var sb = new StringBuilder();
        for (int i = reverse.Count - 1; i >= 0; i--)
        {
            sb.Append(new Rune(reverse[i]));
        }
        lastToken = sb.ToString();
        return true;
    }

    private static int[] ToCodepoints(string s)
    {
        var list = new List<int>(s.Length);
        foreach (Rune r in s.EnumerateRunes())
        {
            list.Add(r.Value);
        }
        return list.ToArray();
    }
}
