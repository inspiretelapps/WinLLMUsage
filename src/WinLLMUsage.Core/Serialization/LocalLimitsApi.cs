using System.Text;
using System.Text.Json;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.Serialization;

public static class LocalLimitsApi
{
    public static byte[] Encode(IReadOnlyList<string> providerIds, LocalUsageApi.State state)
    {
        var providers = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var providerId in providerIds)
        {
            if (!state.Snapshots.TryGetValue(providerId, out var snapshot))
            {
                continue;
            }

            var descriptors = state.LimitDescriptors.GetValueOrDefault(providerId) ?? [];
            providers[providerId] = BuildProvider(snapshot, descriptors, state.GeneratedAt);
        }

        var errors = providerIds
            .Where(id => state.Errors.ContainsKey(id))
            .Select(id => new Dictionary<string, object?>
            {
                ["providerId"] = id,
                ["message"] = state.Errors[id],
            })
            .ToArray();

        var envelope = new Dictionary<string, object?>
        {
            ["errors"] = errors,
            ["generatedAt"] = Iso8601.StringFrom(state.GeneratedAt),
            ["providers"] = providers,
            ["schema"] = AppInfo.LimitsSchema,
        };

        return JsonSerializer.SerializeToUtf8Bytes(envelope, SortedOptions);
    }

    public static string EncodeString(IReadOnlyList<string> providerIds, LocalUsageApi.State state) =>
        Encoding.UTF8.GetString(Encode(providerIds, state));

    private static Dictionary<string, object?> BuildProvider(
        ProviderSnapshot snapshot,
        IReadOnlyList<WidgetDescriptor> descriptors,
        DateTimeOffset generatedAt)
    {
        var expiry = snapshot.RefreshedAt.Add(RefreshSetting.Interval);
        var resources = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            var line = snapshot.Line(descriptor.MetricLabel);
            if (line is null)
            {
                continue;
            }

            foreach (var resource in descriptor.LimitResources)
            {
                var encoded = EncodeResource(resource, line);
                if (encoded is not null)
                {
                    resources[resource.Key] = encoded;
                }
            }
        }

        var provider = new Dictionary<string, object?>
        {
            ["displayName"] = snapshot.DisplayName,
            ["expiresAt"] = Iso8601.StringFrom(expiry),
            ["fetchedAt"] = Iso8601.StringFrom(snapshot.RefreshedAt),
            ["resources"] = resources,
            ["stale"] = generatedAt >= expiry,
        };
        if (snapshot.Plan is not null)
        {
            provider["plan"] = snapshot.Plan;
        }

        return provider;
    }

    private static Dictionary<string, object?>? EncodeResource(LimitResourceDescriptor resource, MetricLine line)
    {
        var kind = resource.Kind == LimitResourceKind.Balance ? "balance" : "consumption";
        if (line is MetricLine.ProgressLine progress
            && resource.Source is LimitResourceSource.ProgressSource or LimitResourceSource.ProgressOrValueSource)
        {
            return ApplyProgress(progress, resource, kind);
        }

        if (line is MetricLine.ValuesLine values)
        {
            MetricKind expectedKind;
            string? expectedLabel;
            switch (resource.Source)
            {
                case LimitResourceSource.ValueSource value:
                    expectedKind = value.Kind;
                    expectedLabel = value.Label;
                    break;
                case LimitResourceSource.ProgressOrValueSource progressOrValue:
                    expectedKind = progressOrValue.Kind;
                    expectedLabel = progressOrValue.Label;
                    break;
                default:
                    return null;
            }

            var metric = values.Items.FirstOrDefault(v =>
                v.Kind == expectedKind && (expectedLabel is null || v.Label == expectedLabel));
            if (metric is null)
            {
                return null;
            }

            var encoded = new Dictionary<string, object?>
            {
                ["kind"] = kind,
                ["unit"] = resource.Unit,
            };
            if (resource.Kind == LimitResourceKind.Balance)
            {
                encoded["available"] = metric.Number;
            }
            else
            {
                encoded["used"] = metric.Number;
            }

            if (values.ExpiriesAt.Count > 0)
            {
                encoded["expiresAt"] = values.ExpiriesAt.OrderBy(d => d).Select(Iso8601.StringFrom).ToArray();
            }

            if (resource.Estimated || metric.Estimated)
            {
                encoded["estimated"] = true;
            }

            return encoded;
        }

        return null;
    }

    private static Dictionary<string, object?> ApplyProgress(MetricLine.ProgressLine progress, LimitResourceDescriptor resource, string kind)
    {
        var boundedLimit = Math.Max(0, progress.Limit);
        var boundedUsed = Math.Max(0, progress.Used);
        var unit = progress.Format switch
        {
            ProgressFormat.PercentFormat => "percent",
            ProgressFormat.DollarsFormat => "usd",
            ProgressFormat.CountFormat count => string.IsNullOrWhiteSpace(count.Suffix) ? resource.Unit : count.Suffix.Trim(),
            _ => resource.Unit,
        };

        var encoded = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["limit"] = boundedLimit,
            ["remaining"] = Math.Max(0, boundedLimit - boundedUsed),
            ["unit"] = unit,
        };
        if (resource.Kind == LimitResourceKind.Consumption)
        {
            encoded["used"] = boundedUsed;
        }
        else
        {
            encoded["available"] = boundedUsed;
        }

        if (boundedLimit > 0)
        {
            encoded["utilization"] = boundedUsed / boundedLimit;
        }

        if (progress.ResetsAt is { } reset)
        {
            encoded["resetsAt"] = Iso8601.StringFrom(reset);
        }

        if (progress.PeriodDurationMs is { } period)
        {
            encoded["windowSeconds"] = period / 1000d;
        }

        if (resource.Estimated)
        {
            encoded["estimated"] = true;
        }

        return encoded;
    }

    private static readonly JsonSerializerOptions SortedOptions = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}
