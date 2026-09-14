namespace WinLLMUsage.Core.Models;

public enum LimitResourceKind
{
    Consumption,
    Balance,
}

public abstract record LimitResourceSource
{
    public static readonly LimitResourceSource Progress = new ProgressSource();

    public static LimitResourceSource Value(MetricKind kind, string? label = null) => new ValueSource(kind, label);

    public static LimitResourceSource ProgressOrValue(MetricKind kind, string? label = null) => new ProgressOrValueSource(kind, label);

    public sealed record ProgressSource : LimitResourceSource;

    public sealed record ValueSource(MetricKind Kind, string? Label) : LimitResourceSource;

    public sealed record ProgressOrValueSource(MetricKind Kind, string? Label) : LimitResourceSource;
}

public sealed record LimitResourceDescriptor(
    string Key,
    LimitResourceKind Kind,
    string Unit,
    LimitResourceSource Source,
    bool Estimated = false);

public enum WidgetDisplayMode
{
    Used,
    Remaining,
}

public enum ResetDisplayMode
{
    Relative,
    Absolute,
}

public enum SessionStartSignal
{
    ZeroUsage,
    MissingResetDate,
}

public enum MenuBarStyle
{
    Text,
    Bars,
}

public sealed class WidgetData
{
    public const string LocalEstimateNote = "Estimated locally, so it may be off";
    public const string CursorUsageHistoryNote = "From your Cursor usage history.";
    public const string NoDataHeadline = "—";
    public const string NoDataSubtitle = "No data";
    public const string FreshSessionTooltip = "Sessions start after you send your first message.";

    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required MetricKind Kind { get; init; }
    public double Used { get; init; }
    public double? Limit { get; set; }
    public string? CountSuffix { get; init; }
    public string? ValuePrefix { get; init; }
    public WidgetDisplayMode DisplayMode { get; set; } = WidgetDisplayMode.Remaining;
    public ResetDisplayMode ResetDisplayMode { get; set; } = ResetDisplayMode.Relative;
    public bool AlwaysShowPacing { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
    public IReadOnlyList<DateTimeOffset> ExpiriesAt { get; set; } = [];
    public bool ShowsResetExpiries { get; init; }
    public IReadOnlyList<string> UnknownModels { get; set; } = [];
    public ModelUsageBreakdown? ModelBreakdown { get; set; }
    public int? PeriodDurationMs { get; set; }
    public string? ValueTextOverride { get; set; }
    public string? SubtitleOverride { get; set; }
    public string? LimitNoun { get; init; }
    public string? UnboundedValueWord { get; init; }
    public string? InfoNote { get; set; }
    public string? ValueTooltipNote { get; init; }
    public bool HasData { get; set; } = true;
    public IReadOnlyList<MetricValue> Values { get; set; } = [];
    public ValueSelection Selection { get; init; } = ValueSelection.All;
    public bool IsUsagePeriod { get; init; }
    public string? TraySuffix { get; init; }
    public SessionStartSignal? SessionStartSignal { get; init; }
    public bool IsChart { get; init; }
    public IReadOnlyList<MetricChartPoint> ChartPoints { get; set; } = [];
    public string? ChartNote { get; set; }

    public bool IsBounded => Limit is not null;

    public IReadOnlyList<MetricValue> SelectedValues => Selection.Apply(Values);

    public double DisplayedValue
    {
        get
        {
            if (DisplayMode == WidgetDisplayMode.Remaining && Limit is { } limit)
            {
                return Math.Max(0, limit - Used);
            }

            return Used;
        }
    }

    public double RemainingFraction
    {
        get
        {
            if (Limit is not { } limit || limit <= 0)
            {
                return 0;
            }

            return Math.Clamp((limit - Used) / limit, 0, 1);
        }
    }
}

public sealed class WidgetDescriptor : IEquatable<WidgetDescriptor>
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string MetricLabel { get; init; }
    public required WidgetData Sample { get; init; }
    public bool Pinnable { get; init; } = true;
    public bool IsSpendTile { get; init; }
    public List<LimitResourceDescriptor> LimitResources { get; init; } = [];
    public UsageHistoryDescriptor? HistoryResource { get; set; }

    public string Title => Sample.Title;

    public WidgetDescriptor ExportingLimit(
        string key,
        string unit,
        LimitResourceKind kind = LimitResourceKind.Consumption,
        LimitResourceSource? source = null,
        bool estimated = false)
    {
        LimitResources.Add(new LimitResourceDescriptor(key, kind, unit, source ?? LimitResourceSource.Progress, estimated));
        return this;
    }

    public WidgetDescriptor ExportingHistory(HistoryScope scope, bool estimatedCost, string sourceNote)
    {
        HistoryResource = new UsageHistoryDescriptor(scope, estimatedCost, sourceNote);
        return this;
    }

    public bool Equals(WidgetDescriptor? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as WidgetDescriptor);

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
}

public sealed record PlacedWidget(Guid Id, string DescriptorId);
