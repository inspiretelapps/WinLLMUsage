using System.Text;
using WinLLMUsage.Core.Accounts;
using WinLLMUsage.Core.Cli;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.Core.Usage;

public sealed class UsageReadResult
{
    public required byte[] Data { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class UsageReader
{
    private readonly WidgetRegistry _registry;
    private readonly ProviderEnablement _enablement;
    private readonly IRefreshCoordinator _refresh;
    private readonly Func<IReadOnlyDictionary<string, Models.ProviderSnapshot>> _snapshots;
    private readonly Func<IReadOnlyDictionary<string, string>> _errors;
    private readonly IClock _clock;

    public UsageReader(
        WidgetRegistry registry,
        ProviderEnablement enablement,
        IRefreshCoordinator refresh,
        Func<IReadOnlyDictionary<string, Models.ProviderSnapshot>> snapshots,
        Func<IReadOnlyDictionary<string, string>> errors,
        IClock clock)
    {
        _registry = registry;
        _enablement = enablement;
        _refresh = refresh;
        _snapshots = snapshots;
        _errors = errors;
        _clock = clock;
    }

    public async Task<UsageReadResult> ReadAsync(string? providerId, bool force, CancellationToken cancellationToken)
    {
        var known = _registry.Providers.Select(p => p.Provider.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string>? matched = null;
        if (providerId is not null)
        {
            matched = known.Where(id => ProviderAccountId.Matches(id, providerId)).ToHashSet(StringComparer.Ordinal);
            if (matched.Count == 0)
            {
                throw new CliUsageException($"Unknown provider: {providerId}");
            }
        }

        var ordered = _registry.OrderedProviderIds(null);
        var enabled = ordered.Where(id => matched?.Contains(id) ?? _enablement.IsEnabled(id)).ToArray();
        if (matched is not null)
        {
            foreach (var id in ordered.Where(matched.Contains))
            {
                await _refresh.RefreshAsync(id, force, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await _refresh.RefreshAllAsync(force, cancellationToken).ConfigureAwait(false);
        }

        var snapshots = _snapshots();
        var errors = _errors();
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = enabled,
            KnownIds = known,
            Snapshots = snapshots,
            LimitDescriptors = _registry.LimitDescriptorsByProvider,
            Errors = errors,
            GeneratedAt = _clock.Now,
        };
        var path = providerId is null ? "/v1/limits" : "/v1/limits/" + providerId;
        var response = LocalUsageApi.Respond("GET", path, state);
        if (response.Body is null)
        {
            throw new CliReadException("local read produced no data");
        }

        var warnings = ordered.Where(id => errors.ContainsKey(id)).Select(id => $"{id}: {errors[id]}").ToArray();
        return new UsageReadResult { Data = response.Body, Warnings = warnings };
    }

    public static string HelpText => CliArgumentParser.Help;
}

public static class UsageReaderStdout
{
    public static byte[] WithTrailingNewline(byte[] data)
    {
        if (data.Length > 0 && data[^1] == (byte)'\n')
        {
            return data;
        }

        var copy = new byte[data.Length + 1];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);
        copy[^1] = (byte)'\n';
        return copy;
    }

    public static string AsString(byte[] data) => Encoding.UTF8.GetString(WithTrailingNewline(data));
}
