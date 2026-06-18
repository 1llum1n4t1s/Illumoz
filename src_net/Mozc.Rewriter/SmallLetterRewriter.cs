using System.Collections.Generic;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/small_letter_rewriter.cc 相当。
// 読みに「^」(上付き)/「_」(下付き)を含むとき、続く数字・記号を
// 上付き/下付き文字へ変換した候補を挿入する。例「x^2」→「x²」、「a_1」→「a₁」。
public sealed class SmallLetterRewriter : IRewriter
{
    private static readonly Dictionary<char, string> Superscript = new()
    {
        ['0'] = "⁰", ['1'] = "¹", ['2'] = "²", ['3'] = "³", ['4'] = "⁴",
        ['5'] = "⁵", ['6'] = "⁶", ['7'] = "⁷", ['8'] = "⁸", ['9'] = "⁹",
        ['+'] = "⁺", ['-'] = "⁻", ['='] = "⁼", ['('] = "⁽", [')'] = "⁾",
    };

    private static readonly Dictionary<char, string> Subscript = new()
    {
        ['0'] = "₀", ['1'] = "₁", ['2'] = "₂", ['3'] = "₃", ['4'] = "₄",
        ['5'] = "₅", ['6'] = "₆", ['7'] = "₇", ['8'] = "₈", ['9'] = "₉",
        ['+'] = "₊", ['-'] = "₋", ['='] = "₌", ['('] = "₍", [')'] = "₎",
    };

    private enum State { Default, SuperAll, SubAll, SuperDigit, SubDigit }

    public bool Rewrite(Segments segments)
    {
        if (segments.ConversionSegmentsSize != 1)
        {
            return false;
        }
        Segment segment = segments.ConversionSegment(0);
        if (segment.CandidatesSize == 0)
        {
            return false;
        }
        if (!TryConvert(segment.Key, out string value))
        {
            return false;
        }

        Candidate baseCand = segment.Get(0);
        segment.InsertCandidate(segment.CandidatesSize, new Candidate
        {
            Key = baseCand.Key,
            Value = value,
            ContentKey = baseCand.ContentKey,
            ContentValue = value,
            Description = "上下付き文字",
            Cost = baseCand.Cost,
        });
        return true;
    }

    // C++ ConvertExpressions の状態機械を移植。変換が起きた(input != value)ときのみ true。
    public static bool TryConvert(string input, out string value)
    {
        var sb = new global::System.Text.StringBuilder(input.Length);
        State state = State.Default;
        foreach (char c in input)
        {
            switch (state)
            {
                case State.Default:
                    if (c == '^') { state = State.SuperAll; }
                    else if (c == '_') { state = State.SubAll; }
                    else { sb.Append(c); }
                    break;
                case State.SuperAll:
                    if (char.IsAsciiDigit(c)) { sb.Append(Superscript[c]); state = State.SuperDigit; }
                    else if (Superscript.TryGetValue(c, out string? s)) { sb.Append(s); state = State.Default; }
                    else { sb.Append('^').Append(c); state = State.Default; }
                    break;
                case State.SubAll:
                    if (char.IsAsciiDigit(c)) { sb.Append(Subscript[c]); state = State.SubDigit; }
                    else if (Subscript.TryGetValue(c, out string? s)) { sb.Append(s); state = State.Default; }
                    else { sb.Append('_').Append(c); state = State.Default; }
                    break;
                case State.SuperDigit:
                    if (char.IsAsciiDigit(c)) { sb.Append(Superscript[c]); }
                    else if (c == '^') { state = State.SuperAll; }
                    else if (c == '_') { state = State.SubAll; }
                    else { sb.Append(c); state = State.Default; }
                    break;
                case State.SubDigit:
                    if (char.IsAsciiDigit(c)) { sb.Append(Subscript[c]); }
                    else if (c == '^') { state = State.SuperAll; }
                    else if (c == '_') { state = State.SubAll; }
                    else { sb.Append(c); state = State.Default; }
                    break;
            }
        }
        if (state == State.SuperAll) { sb.Append('^'); }
        else if (state == State.SubAll) { sb.Append('_'); }

        value = sb.ToString();
        return input != value;
    }
}
