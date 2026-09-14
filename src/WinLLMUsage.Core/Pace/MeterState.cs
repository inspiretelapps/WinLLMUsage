using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Pace;

public enum MeterSeverity
{
    Normal,
    Warning,
    Critical,
}

public abstract record MeterState
{
    public static readonly MeterState NoData = new NoDataState();
    public static readonly MeterState Spent = new SpentState();

    public abstract MeterSeverity? Severity { get; }

    public sealed record NoDataState : MeterState
    {
        public override MeterSeverity? Severity => null;
    }

    public sealed record SpentState : MeterState
    {
        public override MeterSeverity? Severity => MeterSeverity.Critical;
    }

    public sealed record RunningOut(string? Eta, double ProjectedFraction) : MeterState
    {
        public override MeterSeverity? Severity => MeterSeverity.Critical;
    }

    public sealed record CloseToLimit(string Spare, double ProjectedFraction) : MeterState
    {
        public override MeterSeverity? Severity => MeterSeverity.Warning;
    }

    public sealed record Healthy(double ProjectedFraction) : MeterState
    {
        public override MeterSeverity? Severity => MeterSeverity.Normal;
    }

    public sealed record Level(MeterSeverity Band) : MeterState
    {
        public override MeterSeverity? Severity => Band;
    }
}

public static class MeterEvaluator
{
    public static MeterState Evaluate(WidgetData data, DateTimeOffset now)
    {
        if (!data.HasData)
        {
            return MeterState.NoData;
        }

        if (data.Limit is not { } limit || limit <= 0)
        {
            return MeterState.NoData;
        }

        var usedFraction = Math.Clamp(data.Used / limit, 0, 1);
        var remaining = Math.Max(0, limit - data.Used);
        if (remaining <= 0 || Math.Round(remaining, MidpointRounding.AwayFromZero) <= 0 && data.Kind != MetricKind.Dollars)
        {
            if (data.Kind == MetricKind.Percent && Math.Round(data.Used) >= 100)
            {
                return MeterState.Spent;
            }

            if (data.Kind != MetricKind.Percent && remaining <= 0)
            {
                return MeterState.Spent;
            }
        }

        if (data.Kind == MetricKind.Percent && Math.Round(data.Used) >= 100)
        {
            return MeterState.Spent;
        }

        if (data.ResetsAt is { } reset && reset > now)
        {
            var period = data.PeriodDurationMs is { } ms
                ? TimeSpan.FromMilliseconds(ms)
                : TimeSpan.FromHours(5);
            var elapsed = period - (reset - now);
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            var elapsedFraction = period.TotalSeconds <= 0 ? 0 : elapsed.TotalSeconds / period.TotalSeconds;
            if (usedFraction >= 0.05 && elapsedFraction > 0)
            {
                var projected = usedFraction / elapsedFraction;
                if (projected > 1)
                {
                    var remainingTime = reset - now;
                    var eta = remainingTime.TotalSeconds <= 0 ? null : FormatEta(remainingTime);
                    return new MeterState.RunningOut(eta, projected);
                }

                if (projected > 0.9)
                {
                    var spare = (int)Math.Round((1 - projected) * 100);
                    if (spare <= 0)
                    {
                        return new MeterState.RunningOut(null, projected);
                    }

                    return new MeterState.CloseToLimit($"~{spare}% spare", projected);
                }

                return new MeterState.Healthy(projected);
            }
        }

        var usedPercent = (int)Math.Round(usedFraction * 100);
        if (usedPercent >= 90)
        {
            return new MeterState.Level(MeterSeverity.Critical);
        }

        if (usedPercent >= 80)
        {
            return new MeterState.Level(MeterSeverity.Warning);
        }

        return new MeterState.Level(MeterSeverity.Normal);
    }

    private static string FormatEta(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            var hours = (int)remaining.TotalHours;
            var minutes = remaining.Minutes;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"{(int)remaining.TotalMinutes}m";
        }

        return $"{Math.Max(1, (int)remaining.TotalSeconds)}s";
    }
}
