namespace NMS.Application.Calendar;

/// <summary>
/// The repeat rules the calendar understands, a small and safe part of RFC 5545:
/// FREQ=DAILY|WEEKLY|MONTHLY, INTERVAL=n, BYDAY=MO,TU..., UNTIL=yyyymmdd, COUNT=n. Anything else is refused when saving.
/// </summary>
public sealed record RepeatRule(string Frequency, int Interval, IReadOnlyList<DayOfWeek> Days, DateOnly? Until, int? Count)
{
    private static readonly Dictionary<string, DayOfWeek> DayCodes = new()
    {
        ["MO"] = DayOfWeek.Monday, ["TU"] = DayOfWeek.Tuesday, ["WE"] = DayOfWeek.Wednesday, ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday, ["SA"] = DayOfWeek.Saturday, ["SU"] = DayOfWeek.Sunday,
    };

    /// <summary>Hard limit on how far one event can repeat, so a careless rule cannot flood the calendar.</summary>
    public const int MaxOccurrences = 1000;

    public static bool TryParse(string? text, out RepeatRule? rule)
    {
        rule = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string? freq = null;
        var interval = 1;
        var days = new List<DayOfWeek>();
        DateOnly? until = null;
        int? count = null;
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                return false;
            }

            switch (pair[0].ToUpperInvariant())
            {
                case "FREQ" when pair[1].ToUpperInvariant() is "DAILY" or "WEEKLY" or "MONTHLY":
                    freq = pair[1].ToUpperInvariant();
                    break;
                case "INTERVAL" when int.TryParse(pair[1], out var n) && n is >= 1 and <= 52:
                    interval = n;
                    break;
                case "BYDAY":
                    foreach (var code in pair[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!DayCodes.TryGetValue(code.Trim().ToUpperInvariant(), out var day))
                        {
                            return false;
                        }

                        days.Add(day);
                    }

                    break;
                case "UNTIL" when DateOnly.TryParseExact(pair[1], "yyyyMMdd", out var u):
                    until = u;
                    break;
                case "COUNT" when int.TryParse(pair[1], out var c) && c is >= 1 and <= MaxOccurrences:
                    count = c;
                    break;
                default:
                    return false;
            }
        }

        if (freq is null)
        {
            return false;
        }

        rule = new RepeatRule(freq, interval, days.Distinct().ToList(), until, count);
        return true;
    }

    /// <summary>The dates the event falls on, from its first day, never beyond <paramref name="horizon"/>.</summary>
    public IEnumerable<DateOnly> Dates(DateOnly first, DateOnly horizon)
    {
        var produced = 0;
        var limit = Until is { } u && u < horizon ? u : horizon;
        for (var step = 0; produced < MaxOccurrences; step++)
        {
            IEnumerable<DateOnly> batch = Frequency switch
            {
                "DAILY" => [first.AddDays(step * Interval)],
                "MONTHLY" => MonthlyDate(first, step * Interval),
                _ => WeekDates(first, step * Interval),
            };
            var any = false;
            foreach (var date in batch.OrderBy(d => d))
            {
                if (date < first)
                {
                    continue;
                }

                if (date > limit)
                {
                    yield break;
                }

                any = true;
                yield return date;
                if (++produced >= (Count ?? int.MaxValue))
                {
                    yield break;
                }
            }

            if (!any && step * Interval > 3700)
            {
                yield break;
            }
        }
    }

    private IEnumerable<DateOnly> MonthlyDate(DateOnly first, int months)
    {
        var target = first.AddMonths(months);
        if (target.Day == first.Day)
        {
            yield return target;
        }
    }

    private IEnumerable<DateOnly> WeekDates(DateOnly first, int weeks)
    {
        var weekStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7)).AddDays(weeks * 7);
        var days = Days.Count > 0 ? Days : [first.DayOfWeek];
        foreach (var day in days)
        {
            yield return weekStart.AddDays(((int)day + 6) % 7);
        }
    }
}
