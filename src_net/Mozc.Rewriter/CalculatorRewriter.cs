using System.Globalization;
using System.Text;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/calculator_rewriter.cc + calculator/calculator.cc の中核スライス。
// 読みが算術式(例「1+2=」「3*(4-1)」)のとき計算結果を候補に挿入する。
// 全角数字/演算子は半角へ正規化し、+ - * / と括弧・小数・前置符号を再帰下降で評価する。
public sealed class CalculatorRewriter : IRewriter
{
    private const int InsertIndex = 0; // 計算結果は先頭付近に出す。

    // config.use_calculator=false で計算候補を出さない(C++ use_calculator() 相当)。
    public bool Enabled { get; set; } = true;

    public bool Rewrite(Segments segments)
    {
        if (!Enabled)
        {
            return false;
        }
        // 計算は単一変換セグメントのときのみ(C++ も conversion_segments_size()==1 を要求)。
        if (segments.ConversionSegmentsSize != 1)
        {
            return false;
        }
        Segment segment = segments.ConversionSegment(0);
        if (segment.CandidatesSize == 0)
        {
            return false;
        }

        if (!TryCalculate(segment.Key, out string result))
        {
            return false;
        }

        Candidate baseCand = segment.Get(0);
        // 「式=結果」形式と結果のみの 2 候補を挿入(C++ と同じ並び)。
        string expr = NormalizeForDisplay(segment.Key);
        var cands = new[]
        {
            MakeCandidate(baseCand, $"{expr}{result}"),
            MakeCandidate(baseCand, result),
        };
        segment.InsertCandidates(InsertIndex, cands);
        return true;
    }

    private static Candidate MakeCandidate(Candidate baseCand, string value) => new()
    {
        Key = baseCand.Key,
        Value = value,
        ContentKey = baseCand.ContentKey,
        ContentValue = value,
        Description = "計算結果",
        Cost = baseCand.Cost,
    };

    // 表示用に式末尾の「=」を残しつつ全角を半角化する(「1+2=」→「1+2=」)。
    private static string NormalizeForDisplay(string key)
    {
        string n = Normalize(key);
        return n.EndsWith('=') ? n : n + "=";
    }

    // 評価本体。算術式として妥当なら結果文字列を返す。
    public static bool TryCalculate(string rawKey, out string result)
    {
        result = string.Empty;
        string s = Normalize(rawKey).TrimEnd('=');
        if (s.Length == 0)
        {
            return false;
        }
        // 少なくとも 1 つの二項演算子を含むこと(単なる数値は計算扱いしない)。
        if (s.IndexOfAny(new[] { '+', '-', '*', '/' }, 1) < 0)
        {
            return false;
        }

        var parser = new Parser(s);
        if (!parser.TryParse(out double value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            return false;
        }
        result = Format(value);
        return true;
    }

    private static string Format(double value)
    {
        // 整数なら小数点を付けず、それ以外は余分な 0 を落とす。
        if (value == global::System.Math.Floor(value) && global::System.Math.Abs(value) < 1e15)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }
        return value.ToString("0.##########", CultureInfo.InvariantCulture);
    }

    // 全角英数記号→半角、各種乗除記号の統一。
    private static string Normalize(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (char c in key)
        {
            char m = c switch
            {
                >= '０' and <= '９' => (char)(c - '０' + '0'),
                '＋' => '+',
                '－' or '−' or 'ー' => '-',
                '＊' or '×' => '*',
                '／' or '÷' => '/',
                '．' => '.',
                '（' => '(',
                '）' => ')',
                '＝' => '=',
                _ => c,
            };
            sb.Append(m);
        }
        return sb.ToString();
    }

    // + - * / と括弧・小数・単項符号を扱う再帰下降パーサ。
    private sealed class Parser
    {
        private readonly string _s;
        private int _pos;
        private bool _ok = true;

        public Parser(string s) => _s = s;

        public bool TryParse(out double value)
        {
            value = ParseExpr();
            SkipWs();
            return _ok && _pos == _s.Length;
        }

        private void SkipWs()
        {
            while (_pos < _s.Length && _s[_pos] == ' ')
            {
                _pos++;
            }
        }

        private char Peek()
        {
            SkipWs();
            return _pos < _s.Length ? _s[_pos] : '\0';
        }

        // expr := term (('+'|'-') term)*
        private double ParseExpr()
        {
            double v = ParseTerm();
            while (true)
            {
                char op = Peek();
                if (op == '+' || op == '-')
                {
                    _pos++;
                    double r = ParseTerm();
                    v = op == '+' ? v + r : v - r;
                }
                else
                {
                    return v;
                }
            }
        }

        // term := factor (('*'|'/') factor)*
        private double ParseTerm()
        {
            double v = ParseFactor();
            while (true)
            {
                char op = Peek();
                if (op == '*' || op == '/')
                {
                    _pos++;
                    double r = ParseFactor();
                    if (op == '/')
                    {
                        if (r == 0)
                        {
                            _ok = false;
                            return 0;
                        }
                        v /= r;
                    }
                    else
                    {
                        v *= r;
                    }
                }
                else
                {
                    return v;
                }
            }
        }

        // factor := ('+'|'-') factor | '(' expr ')' | number
        private double ParseFactor()
        {
            char c = Peek();
            if (c == '+')
            {
                _pos++;
                return ParseFactor();
            }
            if (c == '-')
            {
                _pos++;
                return -ParseFactor();
            }
            if (c == '(')
            {
                _pos++;
                double v = ParseExpr();
                if (Peek() != ')')
                {
                    _ok = false;
                    return 0;
                }
                _pos++;
                return v;
            }
            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWs();
            int start = _pos;
            while (_pos < _s.Length && (char.IsDigit(_s[_pos]) || _s[_pos] == '.'))
            {
                _pos++;
            }
            if (_pos == start)
            {
                _ok = false;
                return 0;
            }
            if (!double.TryParse(_s.AsSpan(start, _pos - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v))
            {
                _ok = false;
                return 0;
            }
            return v;
        }
    }
}
