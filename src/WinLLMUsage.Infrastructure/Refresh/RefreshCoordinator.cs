using Microsoft.Extensions.Logging;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Cache;

namespace WinLLMUsage.Infrastructure.Refresh;

public sealed class RefreshCoordinator : IRefreshCoordinator
{
    private readonly WidgetRegistry _registry;
    private readonly SnapshotCache _cache;
    private readonly ProviderEnablement _enablement;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Dictionary<string, DateTimeOffset> _failureBackoff = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(4, 4);

    public RefreshCoordinator(
        WidgetRegistry registry,
        SnapshotCache cache,
        ProviderEnablement enablement,
        IClock clock,
        ILogger logger)
    {
        _registry = registry;
        _cache = cache;
        _enablement = enablement;
        _clock = clock;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, ProviderSnapshot> Snapshots =>
        _cache.LoadAll(_registry.Providers.Select(p => p.Provider.Id));

    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    public async Task RefreshAllAsync(bool force, CancellationToken cancellationToken)
    {
        var ids = _registry.OrderedProviderIds(null).Where(_enablement.IsEnabled).ToArray();
        await Task.WhenAll(ids.Select(id => RefreshAsync(id, force, cancellationToken))).ConfigureAwait(false);
    }

    public async Task<ProviderSnapshot> RefreshAsync(string providerId, bool force, CancellationToken cancellationToken)
    {
        var runtime = _registry.Providers.FirstOrDefault(p => p.Provider.Id == providerId)
                      ?? throw new InvalidOperationException($"Unknown provider {providerId}");

        if (!force && _cache.HasStaleAccountStamp(providerId, runtime.IdentityKey))
        {
            force = true;
        }

        if (!force && _cache.IsFresh(providerId))
        {
            return _cache.Load(providerId)!;
        }

        if (!force && _failureBackoff.TryGetValue(providerId, out var until) && until > _clock.Now)
        {
            return _cache.Load(providerId) ?? ProviderSnapshot.Error(runtime.Provider, "Refresh backing off after a recent failure.", ErrorCategory.Network);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RefreshSetting.ProviderDeadline);
            var snapshot = await runtime.RefreshAsync(force, timeout.Token).ConfigureAwait(false);
            if (snapshot.IsError)
            {
                Errors[providerId] = snapshot.Line(MetricLine.ErrorBadgeLabel) is MetricLine.BadgeLine badge
                    ? badge.BadgeText
                    : "Refresh failed";
                _failureBackoff[providerId] = _clock.Now + RefreshSetting.FailureBackoff;
                return _cache.Load(providerId) ?? snapshot;
            }

            Errors.Remove(providerId);
            _failureBackoff.Remove(providerId);
            if (_cache.HasStaleAccountStamp(providerId, runtime.IdentityKey))
            {
                // Drop the foreign-account snapshot by overwriting after a successful refresh only.
            }

            _cache.Store(snapshot, runtime.IdentityKey);
            return snapshot;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var message = $"Refresh timed out after {RefreshSetting.ProviderDeadline.TotalSeconds:0}s";
            Errors[providerId] = message;
            _failureBackoff[providerId] = _clock.Now + RefreshSetting.FailureBackoff;
            return _cache.Load(providerId) ?? ProviderSnapshot.Error(runtime.Provider, message, ErrorCategory.Network);
        }
        finally
        {
            _gate.Release();
        }
    }
}
