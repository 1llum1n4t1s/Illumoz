using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/date_rewriter.cc の中核スライス。読み(きょう/あした/いま 等)に対し
// 相対日付・時刻の各表記候補を挿入する。和暦(AdToEra)は大きな元号表が要るため後続 TODO、
// WEEKDAY(次の曜日)も後続。DATE/MONTH/YEAR(西暦)/CURRENT_TIME/DATE_AND_CURRENT_TIME を移植。
public sealed class DateRewriter : IRewriter
{
    private enum DateType { Date, Year, Month, CurrentTime, DateAndCurrentTime }

    private readonly record struct DateData(string Key, string Value, string Description, int Diff, DateType Type);

    // C++ kDateData の DATE/YEAR/MONTH/TIME 部分(WEEKDAY は除外)。
    private static readonly DateData[] Table =
    {
        new("きょう", "今日", "今日の日付", 0, DateType.Date),
        new("あした", "明日", "明日の日付", 1, DateType.Date),
        new("あす", "明日", "明日の日付", 1, DateType.Date),
        new("さくじつ", "昨日", "昨日の日付", -1, DateType.Date),
        new("きのう", "昨日", "昨日の日付", -1, DateType.Date),
        new("おととい", "一昨日", "2日前の日付", -2, DateType.Date),
        new("おとつい", "一昨日", "2日前の日付", -2, DateType.Date),
        new("いっさくじつ", "一昨日", "2日前の日付", -2, DateType.Date),
        new("さきおととい", "一昨昨日", "3日前の日付", -3, DateType.Date),
        new("あさって", "明後日", "明後日の日付", 2, DateType.Date),
        new("みょうごにち", "明後日", "明後日の日付", 2, DateType.Date),
        new("しあさって", "明明後日", "明明後日の日付", 3, DateType.Date),
        new("ことし", "今年", "今年", 0, DateType.Year),
        new("らいねん", "来年", "来年", 1, DateType.Year),
        new("さくねん", "昨年", "昨年", -1, DateType.Year),
        new("きょねん", "去年", "去年", -1, DateType.Year),
        new("おととし", "一昨年", "一昨年", -2, DateType.Year),
        new("さらいねん", "再来年", "再来年", 2, DateType.Year),
        new("こんげつ", "今月", "今月", 0, DateType.Month),
        new("らいげつ", "来月", "来月", 1, DateType.Month),
        new("せんげつ", "先月", "先月", -1, DateType.Month),
        new("せんせんげつ", "先々月", "先々月", -2, DateType.Month),
        new("さらいげつ", "再来月", "再来月", 2, DateType.Month),
        new("いま", "今", "現在の時刻", 0, DateType.CurrentTime),
        new("じこく", "時刻", "現在の時刻", 0, DateType.CurrentTime),
        new("にちじ", "日時", "現在の日時", 0, DateType.DateAndCurrentTime),
        new("なう", "ナウ", "現在の日時", 0, DateType.DateAndCurrentTime),
    };

    private static readonly string[] WeekDay = { "日", "月", "火", "水", "木", "金", "土" };

    private readonly IClock _clock;

    public DateRewriter(IClock clock) => _clock = clock;

    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int i = 0; i < segments.ConversionSegmentsSize; i++)
        {
            modified |= RewriteSegment(segments.ConversionSegment(i));
        }
        return modified;
    }

    private bool RewriteSegment(Segment segment)
    {
        int dataIdx = global::System.Array.FindIndex(Table, d => d.Key == segment.Key);
        if (dataIdx < 0)
        {
            return false;
        }
        DateData data = Table[dataIdx];
        List<string> conversions = GetConversions(data);
        if (conversions.Count == 0)
        {
            return false;
        }

        const int maxIdx = 10;
        int endIdx = global::System.Math.Min(maxIdx, segment.CandidatesSize);
        int candIdx = 0;
        for (; candIdx < endIdx; candIdx++)
        {
            if (segment.Get(candIdx).Value == data.Value)
            {
                break;
            }
        }
        if (candIdx == endIdx)
        {
            return false;
        }

        Candidate baseCand = segment.Get(candIdx);
        int minIdx = global::System.Math.Min(3, endIdx);
        int insertIdx = global::System.Math.Clamp(candIdx + 1, minIdx, endIdx);
        var newCands = new List<Candidate>(conversions.Count);
        foreach (string value in conversions)
        {
            newCands.Add(new Candidate
            {
                Key = baseCand.Key,
                Value = value,
                ContentKey = baseCand.ContentKey,
                ContentValue = value,
                Description = data.Description,
                Cost = baseCand.Cost,
            });
        }
        segment.InsertCandidates(insertIdx, newCands);
        return true;
    }

    private List<string> GetConversions(DateData data)
    {
        var results = new List<string>();
        global::System.DateTime now = _clock.Now;
        switch (data.Type)
        {
            case DateType.Date:
            {
                global::System.DateTime d = now.Date.AddDays(data.Diff);
                ConvertDateWithYear(d.Year, d.Month, d.Day, results);
                results.Add($"{WeekDay[(int)d.DayOfWeek]}曜日");
                break;
            }
            case DateType.Month:
            {
                global::System.DateTime d = now.Date.AddMonths(data.Diff);
                results.Add($"{d.Month}");
                results.Add($"{d.Month}月");
                break;
            }
            case DateType.Year:
            {
                int year = now.Year + data.Diff;
                results.Add($"{year}");
                results.Add($"{year}年");
                string? era = AdToEra(year);
                if (era != null)
                {
                    results.Add(era);
                }
                break;
            }
            case DateType.CurrentTime:
                ConvertTime(now.Hour, now.Minute, results);
                break;
            case DateType.DateAndCurrentTime:
                results.Add($"{now.Year}/{now.Month:00}/{now.Day:00} {now.Hour,2}:{now.Minute:00}");
                break;
        }
        return results;
    }

    // C++ ConvertDateWithYear: "Y/MM/DD", "Y-MM-DD", "Y年M月D日"(+和暦年)。
    private static void ConvertDateWithYear(int year, int month, int day, List<string> results)
    {
        results.Add($"{year}/{month:00}/{day:00}");
        results.Add($"{year}-{month:00}-{day:00}");
        results.Add($"{year}年{month}月{day}日");
        string? era = AdToEraWithMonth(year, month);
        if (era != null)
        {
            results.Add($"{era}{month}月{day}日");
        }
    }

    // 元号開始(西暦, 開始月, 元号名)。改元月をまたぐ年は月で判定する。
    private static readonly (int Year, int Month, string Name)[] Eras =
    {
        (2019, 5, "令和"),
        (1989, 1, "平成"),
        (1926, 12, "昭和"),
        (1912, 7, "大正"),
        (1868, 1, "明治"),
    };

    // 西暦年→和暦年(例: 2026→令和8年)。改元年の月は考慮しない簡易版(年のみ)。
    public static string? AdToEra(int adYear)
    {
        foreach ((int y, _, string name) in Eras)
        {
            if (adYear >= y)
            {
                int n = adYear - y + 1;
                return n == 1 ? $"{name}元年" : $"{name}{n}年";
            }
        }
        return null;
    }

    // 改元月を考慮した和暦(年月用)。
    private static string? AdToEraWithMonth(int adYear, int month)
    {
        foreach ((int y, int m, string name) in Eras)
        {
            if (adYear > y || (adYear == y && month >= m))
            {
                int n = adYear - y + 1;
                return n == 1 ? $"{name}元年" : $"{name}{n}年";
            }
        }
        return null;
    }

    // C++ ConvertTime: "H:MM", "H時MM分", 半, 午前/午後。
    private static void ConvertTime(int hour, int min, List<string> results)
    {
        results.Add($"{hour}:{min:00}");
        results.Add($"{hour}時{min:00}分");
        if (min == 30)
        {
            results.Add($"{hour}時半");
        }
        if ((hour % 24) * 60 + min < 720)
        {
            results.Add($"午前{hour % 24}時{min}分");
            if (min == 30)
            {
                results.Add($"午前{hour % 24}時半");
            }
        }
        else
        {
            results.Add($"午後{(hour - 12) % 24}時{min}分");
            if (min == 30)
            {
                results.Add($"午後{(hour - 12) % 24}時半");
            }
        }
    }
}
