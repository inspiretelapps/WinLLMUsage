using System.Text;
using System.Text.Json;
using WinLLMUsage.Core.Accounts;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.Serialization;

public static class LocalUsageApi
{
    public sealed class State
    {
        public required IReadOnlyList<string> EnabledOrderedIds { get; init; }

        public required IReadOnlySet<string> KnownIds { get; init; }

        public required IReadOnlyDictionary<string, ProviderSnapshot> Snapshots { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyList<WidgetDescriptor>> LimitDescriptors { get; init; } =
            new Dictionary<string, IReadOnlyList<WidgetDescriptor>>();

        public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();

        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

        public IReadOnlyList<string> MatchingCardIds(string token)
        {
            var normalized = token.ToLowerInvariant();
            return KnownIds
                .Where(id => ProviderAccountId.Matches(id, normalized))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed record Response(int Status, byte[]? Body);

    public static readonly Response Busy = Error(503, "server_busy");

    public static Response Respond(string method, string path, State state)
    {
        if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return new Response(204, null);
        }

        var pathOnly = path.Split('?', 2)[0];
        var segments = pathOnly.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments is ["v1", "limits"])
        {
            if (!IsGet(method))
            {
                return Error(405, "method_not_allowed");
            }

            return new Response(200, LocalLimitsApi.Encode(state.EnabledOrderedIds, state));
        }

        if (segments.Length == 3 && segments[0] == "v1" && segments[1] == "limits")
        {
            if (!IsGet(method))
            {
                return Error(405, "method_not_allowed");
            }

            var providerIds = state.MatchingCardIds(segments[2]);
            if (providerIds.Count == 0)
            {
                return Error(404, "provider_not_found");
            }

            return new Response(200, LocalLimitsApi.Encode(providerIds, state));
        }

        if (segments is ["v1", "usage"])
        {
            if (!IsGet(method))
            {
                return Error(405, "method_not_allowed");
            }

            var snapshots = state.EnabledOrderedIds.Select(id => state.Snapshots.GetValueOrDefault(id)).Where(s => s is not null).Cast<ProviderSnapshot>().ToArray();
            return new Response(200, EncodeUsage(snapshots));
        }

        if (segments.Length == 3 && segments[0] == "v1" && segments[1] == "usage")
        {
            if (!IsGet(method))
            {
                return Error(405, "method_not_allowed");
            }

            var providerIds = state.MatchingCardIds(segments[2]);
            if (providerIds.Count == 0)
            {
                return Error(404, "provider_not_found");
            }

            var snapshots = providerIds.Select(id => state.Snapshots.GetValueOrDefault(id)).Where(s => s is not null).Cast<ProviderSnapshot>().ToArray();
            return new Response(200, EncodeUsage(snapshots));
        }

        return Error(404, "not_found");
    }

    public static string EncodeUsageString(IReadOnlyList<ProviderSnapshot> snapshots) =>
        Encoding.UTF8.GetString(EncodeUsage(snapshots));

    private static bool IsGet(string method) => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

    private static Response Error(int status, string code) =>
        new(status, Encoding.UTF8.GetBytes($$"""{"error":"{{code}}"}"""));

    private static byte[] EncodeUsage(IReadOnlyList<ProviderSnapshot> snapshots)
    {
        var payload = snapshots.Select(EncodeSnapshot).ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(payload, JsonDefaults.Usage);
    }

    private static Dictionary<string, object?> EncodeSnapshot(ProviderSnapshot snapshot) =>
        new()
        {
            ["providerId"] = snapshot.ProviderId,
            ["displayName"] = snapshot.DisplayName,
            ["plan"] = snapshot.Plan,
            ["lines"] = snapshot.Lines.Select(EncodeLine).ToArray(),
            ["fetchedAt"] = Iso8601.StringFrom(snapshot.RefreshedAt),
        };

    private static Dictionary<string, object?> EncodeLine(MetricLine line)
    {
        switch (line)
        {
            case MetricLine.TextLine text:
                return new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["label"] = text.Label,
                    ["value"] = text.Value,
                    ["color"] = text.ColorHex,
                    ["subtitle"] = text.Subtitle,
                };
            case MetricLine.ValuesLine values:
                return new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["label"] = values.Label,
                    ["value"] = MetricFormatter.LegacyCombined(values.Items),
                    ["color"] = values.ColorHex,
                    ["subtitle"] = null,
                    ["resetsAt"] = values.ExpiriesAt.Count == 0 ? null : Iso8601.StringFrom(values.ExpiriesAt.Min()),
                };
            case MetricLine.ProgressLine progress:
                return new Dictionary<string, object?>
                {
                    ["type"] = "progress",
                    ["label"] = progress.Label,
                    ["used"] = progress.Used,
                    ["limit"] = progress.Limit,
                    ["format"] = EncodeFormat(progress.Format),
                    ["resetsAt"] = progress.ResetsAt is { } reset ? Iso8601.StringFrom(reset) : null,
                    ["periodDurationMs"] = progress.PeriodDurationMs,
                    ["color"] = progress.ColorHex,
                };
            case MetricLine.BadgeLine badge:
                return new Dictionary<string, object?>
                {
                    ["type"] = "badge",
                    ["label"] = badge.Label,
                    ["text"] = badge.BadgeText,
                    ["color"] = badge.ColorHex,
                    ["subtitle"] = badge.Subtitle,
                };
            case MetricLine.ChartLine chart:
                return new Dictionary<string, object?>
                {
                    ["type"] = "barChart",
                    ["label"] = chart.Label,
                    ["points"] = chart.Points.Select(p => new Dictionary<string, object?>
                    {
                        ["label"] = p.Label,
                        ["value"] = p.Value,
                        ["valueLabel"] = p.ValueLabel,
                    }).ToArray(),
                    ["note"] = chart.Note,
                    ["color"] = null,
                };
            default:
                throw new InvalidOperationException($"Unknown line {line.GetType().Name}");
        }
    }

    private static Dictionary<string, object?> EncodeFormat(ProgressFormat format) => format switch
    {
        ProgressFormat.PercentFormat => new() { ["kind"] = "percent" },
        ProgressFormat.DollarsFormat => new() { ["kind"] = "dollars" },
        ProgressFormat.CountFormat count => new() { ["kind"] = "count", ["suffix"] = count.Suffix },
        _ => new() { ["kind"] = "percent" },
    };
}
