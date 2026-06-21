using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/fortune_rewriter.cc 相当。読みが「おみくじ」のとき
// 今日の運勢候補(大吉〜凶)を候補末尾に挿入する。
// 運勢の種類は日付依存のしきい値表 + 乱数で決まる。乱数源・日付は注入可能(テスト決定性)。
public sealed class FortuneRewriter : IRewriter
{
    // 運勢の種類。C++ の FortuneType と同順(0=大吉 … 5=凶)。
    public enum FortuneType
    {
        ExcellentLuck = 0, // 大吉
        Luck = 1,          // 吉
        MiddleLuck = 2,    // 中吉
        LittleLuck = 3,    // 小吉
        LuckAtTheEnd = 4,  // 末吉
        Misfortune = 5,    // 凶
    }

    private const int MaxLevel = 100;
    private static readonly int[] NormalLevels = { 20, 40, 60, 80, 90 };
    private static readonly int[] NewYearLevels = { 30, 60, 80, 90, 95 };
    private static readonly int[] MyBirthdayLevels = { 30, 60, 80, 90, 95 };
    private static readonly int[] Friday13Levels = { 10, 25, 40, 55, 70 };

    // [0, MaxLevel) の乱数を返す乱数源。
    private readonly global::System.Func<int> _level;
    // 今日の日付を返す。
    private readonly global::System.Func<global::System.DateTime> _today;

    // 既定は System.Random + 現在日(非決定的)。
    public FortuneRewriter()
    {
        var rng = new global::System.Random();
        _level = () => rng.Next(0, MaxLevel);
        _today = () => global::System.DateTime.Now;
    }

    // テスト用: 乱数と日付を固定/制御する。
    public FortuneRewriter(global::System.Func<int> level, global::System.Func<global::System.DateTime> today)
    {
        _level = level;
        _today = today;
    }

    public bool Rewrite(Segments segments)
    {
        if (segments.ConversionSegmentsSize != 1)
        {
            return false;
        }
        Segment segment = segments.ConversionSegment(0);
        if (segment.Key.Length == 0 || segment.Key != "おみくじ")
        {
            return false;
        }
        if (segment.CandidatesSize == 0)
        {
            return false;
        }

        FortuneType type = DecideFortune();
        return InsertCandidate(type, segment.CandidatesSize, segment);
    }

    private FortuneType DecideFortune()
    {
        global::System.DateTime today = _today();
        int[] levels = NormalLevels;
        if (today.Month == 1 && today.Day == 1)
        {
            levels = NewYearLevels; // 元日はより幸運に。
        }
        else if (today.Month == 3 && today.Day == 3)
        {
            levels = MyBirthdayLevels; // 作者の誕生日。
        }
        else if (today.Day == 13 && today.DayOfWeek == global::System.DayOfWeek.Friday)
        {
            levels = Friday13Levels; // 13日の金曜日。
        }

        int level = _level();
        for (int i = 0; i < NormalLevels.Length; i++)
        {
            if (level <= levels[i])
            {
                return (FortuneType)i;
            }
        }
        return FortuneType.Misfortune;
    }

    private static bool InsertCandidate(FortuneType type, int insertPos, Segment segment)
    {
        Candidate baseCand = segment.Get(0);
        int offset = global::System.Math.Min(insertPos, segment.CandidatesSize);
        Candidate triggerC = segment.Get(offset - 1);

        string value = type switch
        {
            FortuneType.ExcellentLuck => "大吉",
            FortuneType.Luck => "吉",
            FortuneType.MiddleLuck => "中吉",
            FortuneType.LittleLuck => "小吉",
            FortuneType.LuckAtTheEnd => "末吉",
            FortuneType.Misfortune => "凶",
            _ => "凶",
        };

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
            Description = "今日の運勢",
        });
        return true;
    }
}
