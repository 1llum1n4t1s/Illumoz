using System.Text;

namespace Mozc.Converter;

// C++ src/converter/reverse_converter.cc の TryNormalizingKeyAsMathExpression 相当。
// 入力を数式として正規化する。全角数字・数式記号を半角へ畳む(× → *, ÷ → /,
// ・ → /, ー → - など)。数式に属さない文字が含まれたら失敗(false)。
public static class MathExpressionNormalizer
{
    public static bool TryNormalize(string s, out string key)
    {
        var sb = new StringBuilder(s.Length);
        foreach (System.Text.Rune rune in s.EnumerateRunes())
        {
            int c = rune.Value;
            // 半角アラビア数字。
            if (c >= '0' && c <= '9')
            {
                sb.Append((char)c);
                continue;
            }
            // 全角アラビア数字「０」-「９」。
            if (c >= 0xFF10 && c <= 0xFF19)
            {
                sb.Append((char)(c - 0xFF10 + '0'));
                continue;
            }
            switch (c)
            {
                case 0x002B:
                case 0xFF0B: sb.Append('+'); break;          // + ＋
                case 0x002D:
                case 0x30FC: sb.Append('-'); break;          // - ー
                case 0x002A:
                case 0xFF0A:
                case 0x00D7: sb.Append('*'); break;          // * ＊ ×
                case 0x002F:
                case 0xFF0F:
                case 0x30FB:
                case 0x00F7: sb.Append('/'); break;          // / ／ ・ ÷
                case 0x0028:
                case 0xFF08: sb.Append('('); break;          // ( （
                case 0x0029:
                case 0xFF09: sb.Append(')'); break;          // ) ）
                case 0x003D:
                case 0xFF1D: sb.Append('='); break;          // = ＝
                default:
                    key = string.Empty;
                    return false;
            }
        }
        key = sb.ToString();
        return true;
    }
}
