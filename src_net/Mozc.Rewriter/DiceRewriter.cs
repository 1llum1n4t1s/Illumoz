using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/dice_rewriter.cc 相当。読みが「さいころ」のとき 1〜6 の
// ランダムな出目候補を挿入する。乱数源は注入可能(テスト決定性のため)。
public sealed class DiceRewriter : IRewriter
{
    private const int DiceFaces = 6;
    private const int LastCandidateIndex = 8; // 1ページ目の末尾。

    // [1, faces] の出目を返す乱数源。
    private readonly global::System.Func<int> _roll;

    // 既定は System.Random ベース(非決定的)。
    public DiceRewriter()
    {
        var rng = new global::System.Random();
        _roll = () => rng.Next(1, DiceFaces + 1);
    }

    // テスト用: 出目を固定/制御する。
    public DiceRewriter(global::System.Func<int> roll) => _roll = roll;

    public bool Rewrite(Segments segments)
    {
        if (segments.ConversionSegmentsSize != 1)
        {
            return false;
        }
        Segment segment = segments.ConversionSegment(0);
        if (segment.Key.Length == 0 || segment.Key != "さいころ")
        {
            return false;
        }
        if (segment.CandidatesSize == 0)
        {
            return false;
        }

        int insertPos = global::System.Math.Min(LastCandidateIndex, segment.CandidatesSize);
        int face = _roll();
        return InsertCandidate(face, insertPos, segment);
    }

    private static bool InsertCandidate(int topFace, int insertPos, Segment segment)
    {
        Candidate baseCand = segment.Get(0);
        int offset = global::System.Math.Min(insertPos, segment.CandidatesSize);
        Candidate triggerC = segment.Get(offset - 1);

        string value = topFace.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        segment.InsertCandidate(offset, new Candidate
        {
            Lid = triggerC.Lid,
            Rid = triggerC.Rid,
            Cost = triggerC.Cost,
            Value = value,
            ContentValue = value,
            Key = baseCand.Key,
            ContentKey = baseCand.ContentKey,
            Attributes = Candidate.Attribute.NoLearning | Candidate.Attribute.NoVariantsExpansion,
            Description = "出た目の数",
        });
        return true;
    }
}
