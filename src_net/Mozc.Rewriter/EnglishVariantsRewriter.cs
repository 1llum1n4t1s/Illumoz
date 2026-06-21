using System.Collections.Generic;
using System.Globalization;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/english_variants_rewriter.cc の中核(ExpandEnglishVariants)。
// 英単語候補(例「google」)に対して大文字小文字のバリアント(google/Google/GOOGLE)を
// 追加する。content_key がひらがなで content_value が英字、というT13N候補が対象。
public sealed class EnglishVariantsRewriter : IRewriter
{
    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int si = 0; si < segments.ConversionSegmentsSize; si++)
        {
            Segment seg = segments.ConversionSegment(si);
            var existing = new HashSet<string>();
            for (int i = 0; i < seg.CandidatesSize; i++)
            {
                existing.Add(seg.Get(i).Value);
            }

            // 末尾から走査して、英字候補ごとにバリアントを直後へ挿入。
            for (int i = seg.CandidatesSize - 1; i >= 0; i--)
            {
                Candidate c = seg.Get(i);
                if (!IsEnglishCandidate(c))
                {
                    continue;
                }
                if (!ExpandEnglishVariants(c.Value, out List<string> variants))
                {
                    continue;
                }
                int insertAt = i + 1;
                foreach (string v in variants)
                {
                    if (!existing.Add(v))
                    {
                        continue; // 既存と重複は追加しない
                    }
                    seg.InsertCandidate(insertAt++, new Candidate
                    {
                        Key = c.Key,
                        Value = v,
                        ContentKey = c.ContentKey,
                        ContentValue = v,
                        Description = c.Description,
                        Cost = c.Cost,
                    });
                    modified = true;
                }
            }
        }
        return modified;
    }

    // content_value が ASCII 英字を含み、content_key がひらがな(T13N 候補)か。
    private static bool IsEnglishCandidate(Candidate c)
        => IsAsciiAlphaWord(c.ContentValue) && IsHiragana(c.ContentKey);

    // C++ ExpandEnglishVariants 移植。input から大小バリアント列を作る。
    public static bool ExpandEnglishVariants(string input, out List<string> variants)
    {
        variants = new List<string>();
        if (string.IsNullOrEmpty(input) || input.Contains(' '))
        {
            return false;
        }

        string lower = input.ToLowerInvariant();
        string upper = input.ToUpperInvariant();
        string capitalized = Capitalize(input);

        if (lower == upper)
        {
            return false; // 非 ASCII(大小区別なし)
        }

        // "iMac" のような非標準表記は小文字のみ展開。
        if (input != lower && input != upper && input != capitalized)
        {
            variants.Add(lower);
            return true;
        }

        if (input != lower) { variants.Add(lower); }
        if (input != capitalized) { variants.Add(capitalized); }
        if (input != upper) { variants.Add(upper); }
        return true;
    }

    // 先頭大文字 + 残り小文字。
    private static string Capitalize(string s)
    {
        if (s.Length == 0)
        {
            return s;
        }
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    private static bool IsAsciiAlphaWord(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }
        foreach (char c in s)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsHiragana(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }
        foreach (char c in s)
        {
            if (c < 'ぁ' || c > 'ゖ')
            {
                if (c != 'ー') // 長音符は許容
                {
                    return false;
                }
            }
        }
        return true;
    }
}
