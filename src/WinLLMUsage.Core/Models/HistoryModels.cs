using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WinLLMUsage.Core.Models;

public sealed record DailyUsageEntry(string Date, long TotalTokens, double? CostUsd);

public sealed record DailyUsageSeries(IReadOnlyList<DailyUsageEntry> Daily);

public sealed record ModelUsageVariant(string Model, long TotalTokens, double? CostUsd);

public sealed record ModelUsageEntry(
    string Model,
    long TotalTokens,
    double? CostUsd,
    IReadOnlyList<ModelUsageVariant>? Variants = null)
{
    public const string Unattributed = "Unattributed";
    public const string Other = "Other";
}

public sealed record ModelUsageDay(string Date, IReadOnlyList<ModelUsageEntry> Models);

public sealed record ModelUsageSeries(IReadOnlyList<ModelUsageDay> Daily);

public sealed record ModelUsageBreakdown(
    long TotalTokens,
    double? TotalCostUsd,
    IReadOnlyList<ModelUsageEntry> Models,
    string SourceNote);

public sealed record ProviderUsageHistory(
    DailyUsageSeries Series,
    ModelUsageSeries? ModelUsage = null,
    IReadOnlyDictionary<string, IReadOnlySet<string>>? UnknownModelsByDay = null,
    IReadOnlyDictionary<string, IReadOnlySet<string>>? FallbackPricingModelsByDay = null);

public enum HistoryScope
{
    MachineLocal,
    AccountWide,
}

public sealed record UsageHistoryDescriptor(HistoryScope Scope, bool EstimatedCost, string SourceNote);

public sealed class UsageHistoryDocument
{
    public const int PreviousDays = 30;

    [JsonPropertyName("schema")]
    public string Schema { get; set; } = AppInfo.HistorySchemaV1;

    [JsonPropertyName("deviceID")]
    public string DeviceId { get; set; } = "";

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderUsageHistory> Providers { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("identities")]
    public Dictionary<string, string>? Identities { get; set; }

    public void Validate()
    {
        if (Schema is not AppInfo.HistorySchemaV1 and not AppInfo.HistorySchemaV2)
        {
            throw new UsageHistoryDocumentException("unsupported_schema", "This device wrote a newer usage-history format.");
        }

        if (string.IsNullOrWhiteSpace(DeviceId) || string.IsNullOrWhiteSpace(DeviceName))
        {
            throw new UsageHistoryDocumentException("invalid_device", "The synced device identity is invalid.");
        }

        var providerPattern = Schema == AppInfo.HistorySchemaV1
            ? @"^[a-z0-9][a-z0-9-]*$"
            : @"^[a-z0-9][a-z0-9-]*(?:@[a-f0-9]{8})?$";

        foreach (var (providerId, history) in Providers)
        {
            if (!Regex.IsMatch(providerId, providerPattern))
            {
                throw new UsageHistoryDocumentException("invalid_provider", "The synced provider identifier is invalid.");
            }

            var days = new HashSet<string>(StringComparer.Ordinal);
            foreach (var day in history.Series.Daily)
            {
                if (!days.Add(day.Date))
                {
                    throw new UsageHistoryDocumentException("duplicate_day", day.Date);
                }

                ValidateDay(day.Date, day.TotalTokens, day.CostUsd);
            }
        }
    }

    public static bool IsDayKey(string value)
    {
        var parts = value.Split('-');
        if (parts.Length != 3 || parts[0].Length != 4 || parts[1].Length != 2 || parts[2].Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month)
            || !int.TryParse(parts[2], out var day)
            || month is < 1 or > 12)
        {
            return false;
        }

        return DateTime.DaysInMonth(year, month) >= day;
    }

    public static string FormatDay(DateTimeOffset date, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(date, timeZone);
        return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void ValidateDay(string date, long tokens, double? cost)
    {
        if (!IsDayKey(date) || tokens < 0 || (cost is { } c && (!double.IsFinite(c) || c < 0)))
        {
            throw new UsageHistoryDocumentException("invalid_value", date);
        }
    }
}

public sealed class UsageHistoryDocumentException : Exception
{
    public UsageHistoryDocumentException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
