using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Catalog;

public static class WidgetDescriptorFactory
{
    public static WidgetDescriptor Percent(
        string id,
        Provider provider,
        string title,
        string? metricLabel = null,
        SessionStartSignal? sessionStartSignal = null)
    {
        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = MetricKind.Percent,
            Used = 0,
            Limit = 100,
            SessionStartSignal = sessionStartSignal,
        });
    }

    public static WidgetDescriptor BoundedDollars(
        string id,
        Provider provider,
        string title,
        double limit,
        string? metricLabel = null,
        string? limitNoun = null,
        string? valueWord = null)
    {
        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = MetricKind.Dollars,
            Used = 0,
            Limit = limit,
            LimitNoun = limitNoun,
            UnboundedValueWord = valueWord,
        });
    }

    public static WidgetDescriptor BoundedCount(
        string id,
        Provider provider,
        string title,
        double limit,
        string suffix,
        string? metricLabel = null,
        int? periodDurationMs = null)
    {
        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = MetricKind.Count,
            Used = 0,
            Limit = limit,
            CountSuffix = suffix,
            PeriodDurationMs = periodDurationMs,
        });
    }

    public static WidgetDescriptor Values(
        string id,
        Provider provider,
        string title,
        string? metricLabel = null,
        ValueSelection? selection = null,
        string? valueWord = null,
        bool isUsagePeriod = false,
        string? traySuffix = null,
        bool showsResetExpiries = false)
    {
        var chosen = selection ?? ValueSelection.All;
        var kind = MetricKind.Dollars;
        if (chosen is ValueSelection)
        {
            // Keep Swift's count-only seeding: kind is unused for values rendering.
            kind = MetricKind.Dollars;
        }

        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = kind,
            Used = 0,
            Limit = null,
            UnboundedValueWord = valueWord,
            Selection = chosen,
            IsUsagePeriod = isUsagePeriod,
            TraySuffix = traySuffix,
            ShowsResetExpiries = showsResetExpiries,
        });
    }

    public static WidgetDescriptor Combined(
        string id,
        Provider provider,
        string title,
        string? metricLabel = null,
        bool isUsagePeriod = false) =>
        Values(id, provider, title, metricLabel, ValueSelection.All, isUsagePeriod: isUsagePeriod);

    public static WidgetDescriptor DollarBalance(string id, Provider provider, string title, string valueWord, string? metricLabel = null)
    {
        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = MetricKind.Dollars,
            Used = 0,
            Limit = null,
            UnboundedValueWord = valueWord,
        });
    }

    public static WidgetDescriptor Badge(string id, Provider provider, string title, string? metricLabel = null)
    {
        return Make(id, provider, metricLabel ?? title, new WidgetData
        {
            Title = title,
            Icon = provider.Icon,
            Kind = MetricKind.Count,
            Used = 0,
            Limit = null,
        });
    }

    public static WidgetDescriptor UsageTrend(Provider provider)
    {
        return Make($"{provider.Id}.trend", provider, "Usage Trend", new WidgetData
        {
            Title = "Usage Trend",
            Icon = provider.Icon,
            Kind = MetricKind.Count,
            Used = 0,
            Limit = null,
            IsChart = true,
        }, pinnable: false);
    }

    public static IReadOnlyList<WidgetDescriptor> SpendTiles(Provider provider, string? valueTooltipNote = null)
    {
        WidgetDescriptor[] descriptors =
        [
            Combined($"{provider.Id}.today", provider, "Today", isUsagePeriod: true),
            Combined($"{provider.Id}.yesterday", provider, "Yesterday", isUsagePeriod: true),
            Combined($"{provider.Id}.last30", provider, "Last 30 Days", isUsagePeriod: true),
        ];

        return descriptors.Select(descriptor => new WidgetDescriptor
        {
            Id = descriptor.Id,
            ProviderId = descriptor.ProviderId,
            MetricLabel = descriptor.MetricLabel,
            Sample = new WidgetData
            {
                Title = descriptor.Sample.Title,
                Icon = descriptor.Sample.Icon,
                Kind = descriptor.Sample.Kind,
                Used = 0,
                Limit = null,
                Selection = ValueSelection.All,
                IsUsagePeriod = true,
                ValueTooltipNote = valueTooltipNote,
            },
            Pinnable = descriptor.Pinnable,
            IsSpendTile = true,
        }).ToArray();
    }

    private static WidgetDescriptor Make(string id, Provider provider, string metricLabel, WidgetData sample, bool pinnable = true) =>
        new()
        {
            Id = id,
            ProviderId = provider.Id,
            MetricLabel = metricLabel,
            Sample = sample,
            Pinnable = pinnable,
        };
}
