namespace WinLLMUsage.Core.Time;

public static class MetricPeriod
{
    public const int SessionMs = 18_000_000;
    public const int DayMs = 86_400_000;
    public const int WeekMs = 604_800_000;
    public const long MonthMs = 2_592_000_000L;

    public static readonly TimeSpan Session = TimeSpan.FromMilliseconds(SessionMs);
    public static readonly TimeSpan Day = TimeSpan.FromMilliseconds(DayMs);
    public static readonly TimeSpan Week = TimeSpan.FromMilliseconds(WeekMs);
    public static readonly TimeSpan Month = TimeSpan.FromMilliseconds(MonthMs);
}
