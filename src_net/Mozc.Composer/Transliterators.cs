using Mozc.Base;

namespace Mozc.Composer;

// C++ composer::Transliterators 相当(簡略)。CharChunk の (raw, converted) から
// 各表記(ひらがな/カタカナ/半角全角 ASCII 等)の文字列を生成する。
// 注: 本家の HIRAGANA/FULL_KATAKANA は CharacterFormManager(config)で字形正規化するが、
// 現状は config 非依存=正規化なしの恒等で扱う(後続で CharacterFormManager 移植時に対応)。
public enum Transliterator
{
    Local,            // CharChunk 自身の transliterator に委譲
    ConversionString, // converted をそのまま
    RawString,        // raw をそのまま
    Hiragana,
    FullKatakana,
    HalfKatakana,
    FullAscii,
    HalfAscii,
}

public static class Transliterators
{
    public static string Apply(Transliterator t12r, string raw, string converted)
    {
        switch (t12r)
        {
            case Transliterator.RawString:
                return raw;
            case Transliterator.Hiragana:
                return JapaneseUtil.HalfWidthToFullWidth(converted);
            case Transliterator.FullKatakana:
                return JapaneseUtil.HalfWidthToFullWidth(JapaneseUtil.HiraganaToKatakana(converted));
            case Transliterator.HalfKatakana:
                return JapaneseUtil.FullWidthToHalfWidth(JapaneseUtil.HiraganaToKatakana(converted));
            case Transliterator.FullAscii:
                return JapaneseUtil.HalfWidthAsciiToFullWidthAscii(raw.Length == 0 ? converted : raw);
            case Transliterator.HalfAscii:
                return JapaneseUtil.FullWidthAsciiToHalfWidthAscii(raw.Length == 0 ? converted : raw);
            case Transliterator.ConversionString:
            default:
                return converted;
        }
    }
}
