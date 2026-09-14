namespace WinLLMUsage.Core.Models;

public sealed record ProviderLink(string Label, string Url);

public sealed record Provider(string Id, string DisplayName, string Icon, IReadOnlyList<ProviderLink>? Links = null)
{
    public IReadOnlyList<ProviderLink> VisibleLinks =>
        (Links ?? [])
        .Select(link => new ProviderLink(link.Label.Trim(), link.Url.Trim()))
        .Where(link =>
            link.Label.Length > 0
            && link.Url.Length > 0
            && (link.Url.StartsWith("https://", StringComparison.Ordinal) || link.Url.StartsWith("http://", StringComparison.Ordinal)))
        .ToArray();
}

public sealed record ProviderSnapshot(
    string ProviderId,
    string DisplayName,
    IReadOnlyList<MetricLine> Lines,
    DateTimeOffset RefreshedAt,
    string? Plan = null,
    ProviderUsageHistory? UsageHistory = null,
    string? Warning = null,
    ErrorCategory? ErrorCategory = null)
{
    public MetricLine? Line(string label) => Lines.FirstOrDefault(line => line.Label == label);

    public bool IsError => ErrorCategory is not null || Lines.Any(line => line.IsError);

    public static ProviderSnapshot Make(
        Provider provider,
        string? plan,
        IReadOnlyList<MetricLine> lines,
        DateTimeOffset refreshedAt,
        ProviderUsageHistory? usageHistory = null,
        string? warning = null) =>
        new(provider.Id, provider.DisplayName, lines, refreshedAt, plan, usageHistory, warning);

    public static ProviderSnapshot Error(Provider provider, string message, ErrorCategory category = Models.ErrorCategory.Other) =>
        new(
            provider.Id,
            provider.DisplayName,
            [MetricLine.Badge(MetricLine.ErrorBadgeLabel, message, "#EF4444")],
            DateTimeOffset.UtcNow,
            ErrorCategory: category);
}
