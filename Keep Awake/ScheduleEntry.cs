namespace Keep_Awake;

public class ScheduleEntry
{
    public DayOfWeek Day   { get; set; } = DayOfWeek.Monday;
    public TimeSpan  Start { get; set; } = new(9, 0, 0);
    public TimeSpan  End   { get; set; } = new(17, 0, 0);
}
