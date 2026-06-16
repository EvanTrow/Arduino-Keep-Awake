namespace Keep_Awake;

public static class ScheduleManager
{
    public static bool IsActiveNow(IEnumerable<ScheduleEntry> entries)
    {
        var now = DateTime.Now;
        var tod = now.TimeOfDay;
        var day = now.DayOfWeek;

        foreach (var e in entries)
        {
            if (e.Day != day) continue;
            if (e.Start <= e.End)
            {
                if (tod >= e.Start && tod < e.End) return true;
            }
            else
            {
                if (tod >= e.Start || tod < e.End) return true;
            }
        }
        return false;
    }

    public static string NextEventText(IReadOnlyList<ScheduleEntry> entries)
    {
        if (entries.Count == 0) return "No schedule configured";

        var now    = DateTime.Now;
        var tod    = now.TimeOfDay;
        var today  = now.DayOfWeek;
        bool active = IsActiveNow(entries);

        for (int d = 0; d < 7; d++)
        {
            var day = (DayOfWeek)(((int)today + d) % 7);
            foreach (var e in entries.Where(e => e.Day == day).OrderBy(e => e.Start))
            {
                if (active)
                {
                    if (d == 0 && e.Start <= tod && tod < e.End)
                        return $"Active until {Fmt(e.End)}";
                }
                else
                {
                    if (d == 0 && e.Start <= tod) continue;
                    return d == 0
                        ? $"Next active at {Fmt(e.Start)} today"
                        : $"Next active {day} at {Fmt(e.Start)}";
                }
            }
        }
        return active ? "Active" : "No upcoming active period";
    }

    public static string Summarise(IReadOnlyList<ScheduleEntry> entries)
    {
        if (entries.Count == 0) return "No schedule entries – always paused";

        var grouped = entries
            .GroupBy(e => e.Day)
            .OrderBy(g => (int)g.Key)
            .Select(g =>
            {
                string day    = g.Key.ToString()[..3];
                string ranges = string.Join(", ", g.Select(e => $"{Fmt(e.Start)}–{Fmt(e.End)}"));
                return $"{day}: {ranges}";
            });

        return string.Join("  ·  ", grouped);
    }

    private static string Fmt(TimeSpan t)
    {
        int h = t.Hours, m = t.Minutes;
        string ampm = h < 12 ? "AM" : "PM";
        int h12 = h % 12; if (h12 == 0) h12 = 12;
        return m == 0 ? $"{h12} {ampm}" : $"{h12}:{m:D2} {ampm}";
    }
}
