namespace Mozc.Base;

// C++ src/base/util.cc の文字受理判定など。
public static class CharacterUtil
{
    // 候補(変換結果)として表示してよいコードポイントか。
    // C++ Util::IsAcceptableCharacterAsCandidate 相当。
    // 範囲外・制御文字(C0/C1, DEL)・双方向制御文字を拒否する。
    public static bool IsAcceptableCharacterAsCandidate(int cp)
    {
        // Unicode 範囲外 / サロゲート(非スカラー値)。
        if (cp < 0 || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
        {
            return false;
        }
        // 制御文字(C0 / DEL〜C1)。
        if (cp < 0x20 || (cp >= 0x7F && cp <= 0x9F))
        {
            return false;
        }
        // 双方向テキスト制御文字。
        if (cp == 0x061C || cp == 0x200E || cp == 0x200F
            || (cp >= 0x202A && cp <= 0x202E)
            || (cp >= 0x2066 && cp <= 0x2069))
        {
            return false;
        }
        return true;
    }
}
