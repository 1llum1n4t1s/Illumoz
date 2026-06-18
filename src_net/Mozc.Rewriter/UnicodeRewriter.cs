using System.Globalization;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/unicode_rewriter.cc 相当。
// 読みが「U+XXXX」形式(16進コードポイント)のとき、その文字を候補に挿入する。
// 例「U+3042」→「あ」。受理できないコードポイント(制御/サロゲート/範囲外/非文字)は無視。
public sealed class UnicodeRewriter : IRewriter
{
    private const int InsertIndex = 5; // C++ AddCandidate(key, value, 5, segment) と同じ。

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
        int index = global::System.Math.Min(InsertIndex, segment.CandidatesSize);
        segment.InsertCandidate(index, new Candidate
        {
            Key = baseCand.Key,
            Value = value,
            ContentKey = baseCand.ContentKey,
            ContentValue = value,
            Description = $"Unicode 変換 ({segment.Key})",
            Cost = baseCand.Cost,
        });
        return true;
    }

    // 「U+XXXX」(2文字の接頭辞 + 1〜6桁の16進)を文字へ。成否を返す。
    public static bool TryConvert(string key, out string value)
    {
        value = string.Empty;
        if (key.Length < 3 || key.Length > 8)
        {
            return false;
        }
        if (key[0] != 'U' || key[1] != '+')
        {
            return false;
        }
        string hex = key.Substring(2);
        foreach (char c in hex)
        {
            if (!global::System.Uri.IsHexDigit(c))
            {
                return false;
            }
        }
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codepoint))
        {
            return false;
        }
        if (!IsAcceptableCodepoint(codepoint))
        {
            return false;
        }
        value = char.ConvertFromUtf32(codepoint);
        return true;
    }

    // 逆変換(C++ RewriteToUnicodeCharFormat 相当): 単一文字を「U+XXXX」表記へ。
    // 1グラフェム(コードポイント1個)でなければ null。サロゲートペアも扱う。
    public static string? ToUnicodeFormat(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        // 単一コードポイント(BMP 1文字 or サロゲートペア2文字)のみ受理。
        int codepoint;
        if (text.Length == 1 && !char.IsSurrogate(text[0]))
        {
            codepoint = text[0];
        }
        else if (text.Length == 2 && char.IsHighSurrogate(text[0]) && char.IsLowSurrogate(text[1]))
        {
            codepoint = char.ConvertToUtf32(text[0], text[1]);
        }
        else
        {
            return null;
        }
        // C++ absl::StrFormat("U+%04X") 相当(最低4桁ゼロ埋め)。
        return "U+" + codepoint.ToString("X4", CultureInfo.InvariantCulture);
    }

    // C++ Util::IsAcceptableCharacterAsCandidate 相当の保守的判定。
    private static bool IsAcceptableCodepoint(int cp)
    {
        if (cp < 0 || cp > 0x10FFFF)
        {
            return false; // Unicode 範囲外
        }
        if (cp >= 0xD800 && cp <= 0xDFFF)
        {
            return false; // サロゲート
        }
        if (cp < 0x20)
        {
            return false; // 制御文字(C0)
        }
        if (cp >= 0x7F && cp <= 0x9F)
        {
            return false; // 制御文字(DEL/C1)
        }
        // 非文字(U+FDD0..U+FDEF, 各面の末尾 FFFE/FFFF)。
        if (cp >= 0xFDD0 && cp <= 0xFDEF)
        {
            return false;
        }
        if ((cp & 0xFFFE) == 0xFFFE)
        {
            return false;
        }
        return true;
    }
}
