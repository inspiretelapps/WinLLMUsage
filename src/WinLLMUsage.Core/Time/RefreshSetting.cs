namespace WinLLMUsage.Core.Time;

/// <summary>Single source of truth for the five-minute cache/refresh cadence.</summary>
public static class RefreshSetting
{
    public const int DefaultMinutes = 5;
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(DefaultMinutes);
    public static readonly TimeSpan StalenessHint = TimeSpan.FromMinutes(DefaultMinutes * 2);
    public static readonly TimeSpan ProviderDeadline = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SlowRefreshLog = TimeSpan.FromSeconds(10);
}
