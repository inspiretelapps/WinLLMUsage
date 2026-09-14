using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.Core.Pace;

public enum NotificationMilestone
{
    UnderTenPercent,
    HealthyToClose,
    CloseToRunningOut,
}

public enum PaceBucket
{
    None,
    Healthy,
    Close,
    RunningOut,
    Spent,
}

public sealed class NotificationState
{
    public PaceBucket PreviousBucket { get; set; } = PaceBucket.None;
    public bool Primed { get; set; }
    public bool WasUnderTenPercent { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
    public HashSet<NotificationMilestone> FiredMilestones { get; } = [];
}

public static class QuotaNotificationLogic
{
    public static PaceBucket BucketFor(MeterState state) => state switch
    {
        MeterState.Healthy _ => PaceBucket.Healthy,
        MeterState.CloseToLimit _ => PaceBucket.Close,
        MeterState.RunningOut _ => PaceBucket.RunningOut,
        MeterState.SpentState _ => PaceBucket.Spent,
        _ => PaceBucket.None,
    };

    public static bool ResetWindowAdvanced(DateTimeOffset? current, DateTimeOffset? previous)
    {
        if (current is null || previous is null)
        {
            return false;
        }

        return (current.Value - previous.Value).Duration() > TimeSpan.FromSeconds(1)
               && current > previous;
    }

    public static (IReadOnlyList<NotificationMilestone> Fire, NotificationState NewState) Transitions(
        MeterState state,
        double remainingFraction,
        DateTimeOffset? resetsAt,
        NotificationState previous,
        PaceNotificationToggles toggles)
    {
        var next = new NotificationState
        {
            PreviousBucket = BucketFor(state),
            Primed = true,
            WasUnderTenPercent = remainingFraction < 0.10,
            ResetsAt = resetsAt,
        };
        foreach (var milestone in previous.FiredMilestones)
        {
            next.FiredMilestones.Add(milestone);
        }

        if (state is MeterState.NoDataState)
        {
            return ([], previous);
        }

        var fire = new List<NotificationMilestone>();
        if (!previous.Primed)
        {
            return (fire, next);
        }

        if (ResetWindowAdvanced(resetsAt, previous.ResetsAt))
        {
            next.FiredMilestones.Clear();
            previous = new NotificationState { Primed = true, ResetsAt = previous.ResetsAt };
        }

        var bucket = next.PreviousBucket;
        if (toggles.HealthyToClose
            && previous.PreviousBucket == PaceBucket.Healthy
            && bucket == PaceBucket.Close
            && next.FiredMilestones.Add(NotificationMilestone.HealthyToClose))
        {
            fire.Add(NotificationMilestone.HealthyToClose);
        }

        if (toggles.CloseToRunningOut
            && previous.PreviousBucket is PaceBucket.Healthy or PaceBucket.Close
            && bucket is PaceBucket.RunningOut or PaceBucket.Spent
            && next.FiredMilestones.Add(NotificationMilestone.CloseToRunningOut))
        {
            fire.Add(NotificationMilestone.CloseToRunningOut);
        }

        if (toggles.UnderTenPercent
            && remainingFraction < 0.10
            && !previous.WasUnderTenPercent
            && next.FiredMilestones.Add(NotificationMilestone.UnderTenPercent))
        {
            fire.Add(NotificationMilestone.UnderTenPercent);
        }

        return (fire, next);
    }

    public static (string Title, string Body) Copy(NotificationMilestone milestone) => milestone switch
    {
        NotificationMilestone.UnderTenPercent => ("Almost Out", "Under 10% usage remaining for this window."),
        NotificationMilestone.HealthyToClose => ("Cutting It Close", "Projected to finish close to your limit."),
        NotificationMilestone.CloseToRunningOut => ("Will Run Out", "Projected to finish before the limit resets."),
        _ => ("Quota", ""),
    };
}
