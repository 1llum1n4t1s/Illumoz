using System.Text;
using Mozc.Base;

namespace Mozc.Dictionary;

// C++ src/dictionary/user_dictionary_util.cc 相当(検証・正規化部)。
// ユーザー辞書エントリの読み/単語/コメントの妥当性検査と読みの正規化を行う。
public static class UserDictionaryUtil
{
    // C++ ExtendedErrorCode の検証関連サブセット。
    public enum ValidationResult
    {
        Ok,
        ReadingEmpty,
        ReadingTooLong,
        ReadingContainsInvalidChar,
        WordEmpty,
        WordTooLong,
        WordContainsInvalidChar,
        CommentTooLong,
        CommentContainsInvalidChar,
    }

    private const int MaxStringSize = 300;
    private static readonly char[] InvalidChars = { '\n', '\r', '\t' };

    // 読みを正規化する: 全角ASCII→半角ASCII、半角カナ→全角カナ、カタカナ→ひらがな。
    public static string NormalizeReading(string input)
    {
        string tmp1 = JapaneseUtil.FullWidthAsciiToHalfWidthAscii(input);
        string tmp2 = JapaneseUtil.HalfWidthKatakanaToFullWidthKatakana(tmp1);
        return JapaneseUtil.KatakanaToHiragana(tmp2);
    }

    // 正規化後の読みが許容文字種のみか。
    public static bool IsValidReading(string reading)
        => IsValidNormalizedReading(NormalizeReading(reading));

    public static bool IsTooLongString(string str)
        => Encoding.UTF8.GetByteCount(str) > MaxStringSize;

    public static bool ContainsInvalidChars(string str)
        => str.IndexOfAny(InvalidChars) >= 0;

    // エントリ(読み/単語/コメント)を検証する。最初に見つかった問題を返す。
    public static ValidationResult ValidateEntry(string reading, string word, string comment)
    {
        if (string.IsNullOrEmpty(reading))
        {
            return ValidationResult.ReadingEmpty;
        }
        if (IsTooLongString(reading))
        {
            return ValidationResult.ReadingTooLong;
        }
        if (ContainsInvalidChars(reading))
        {
            return ValidationResult.ReadingContainsInvalidChar;
        }

        if (string.IsNullOrEmpty(word))
        {
            return ValidationResult.WordEmpty;
        }
        if (IsTooLongString(word))
        {
            return ValidationResult.WordTooLong;
        }
        if (ContainsInvalidChars(word))
        {
            return ValidationResult.WordContainsInvalidChar;
        }

        if (IsTooLongString(comment))
        {
            return ValidationResult.CommentTooLong;
        }
        if (ContainsInvalidChars(comment))
        {
            return ValidationResult.CommentContainsInvalidChar;
        }

        return ValidationResult.Ok;
    }

    // C++ InternalValidateNormalizedReading 相当の許容文字範囲チェック。
    private static bool IsValidNormalizedReading(string reading)
    {
        foreach (Rune rune in reading.EnumerateRunes())
        {
            int c = rune.Value;
            if (!InRange(c, 0x0021, 0x007E)   // Basic Latin(ASCII)
                && !InRange(c, 0x3041, 0x3096) // ひらがな
                && !InRange(c, 0x309B, 0x309C) // 濁点・半濁点
                && !InRange(c, 0x30FB, 0x30FC) // 中黒・長音符
                && !InRange(c, 0x3001, 0x3002) // 句読点
                && !InRange(c, 0x300C, 0x300F) // 鉤括弧
                && !InRange(c, 0x301C, 0x301C)) // 波ダッシュ
            {
                return false;
            }
        }
        return true;
    }

    private static bool InRange(int c, int lo, int hi) => c >= lo && c <= hi;
}
