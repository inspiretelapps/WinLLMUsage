using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Pricing;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Api;
using WinLLMUsage.Infrastructure.Cache;
using WinLLMUsage.Infrastructure.Http;
using WinLLMUsage.Infrastructure.Logging;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Infrastructure.Refresh;
using WinLLMUsage.Infrastructure.Secrets;
using WinLLMUsage.Infrastructure.Settings;
using WinLLMUsage.Infrastructure.Sync;
using WinLLMUsage.Providers.Catalog;
using WinLLMUsage.Windows;

namespace WinLLMUsage.Host;

public sealed class UsageApplication : IAsyncDisposable
{
    public required IHost Host { get; init; }
    public required AppPaths Paths { get; init; }
    public required ISettingsStore Settings { get; init; }
    public required ProviderEnablement Enablement { get; init; }
    public required WidgetRegistry Registry { get; init; }
    public required IRefreshCoordinator Refresh { get; init; }
    public required SnapshotCache Cache { get; init; }
    public required LocalUsageServer Api { get; init; }

    public static UsageApplication Create(bool cliCacheFreshness, string[]? args = null)
    {
        var paths = new AppPaths();
        paths.EnsureCreated();
        var settings = new JsonSettingsStore(paths.SettingsFile);
        SettingsMigrator.Migrate(settings);
        ISecretStore secrets = OperatingSystem.IsWindows()
            ? new DpapiSecretStore(paths.SecretsDirectory)
            : new FileSecretStore(paths.SecretsDirectory);

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? []);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new FileLoggerProvider(paths.LogFile));
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton<ISettingsStore>(settings);
        builder.Services.AddSingleton(secrets);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IPricingService>(_ => new ModelPricingStore(settings.Get<string>(AppSettings.CodexFallbackModelKey)));
        builder.Services.AddSingleton(sp => new HttpTransport(settings.Get<ProxySettings>("proxy") ?? new ProxySettings()));
        builder.Services.AddSingleton<IHttpTransport>(sp => sp.GetRequiredService<HttpTransport>());
        builder.Services.AddSingleton(sp => new WidgetRegistry(ProviderCatalog.Create(
            sp.GetRequiredService<IHttpTransport>(),
            secrets,
            paths,
            sp.GetRequiredService<IClock>(),
            settings)));
        builder.Services.AddSingleton(_ => new ProviderEnablement(settings));
        builder.Services.AddSingleton(sp => new SnapshotCache(paths.SnapshotsFile, sp.GetRequiredService<IClock>(), cliCacheFreshness));
        builder.Services.AddSingleton<IRefreshCoordinator>(sp => new RefreshCoordinator(
            sp.GetRequiredService<WidgetRegistry>(),
            sp.GetRequiredService<SnapshotCache>(),
            sp.GetRequiredService<ProviderEnablement>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("refresh")));
        builder.Services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<WidgetRegistry>();
            var cache = sp.GetRequiredService<SnapshotCache>();
            var enablement = sp.GetRequiredService<ProviderEnablement>();
            var coordinator = (RefreshCoordinator)sp.GetRequiredService<IRefreshCoordinator>();
            return new LocalUsageServer(
                () => new LocalUsageApi.State
                {
                    EnabledOrderedIds = registry.OrderedProviderIds(null).Where(enablement.IsEnabled).ToArray(),
                    KnownIds = registry.Providers.Select(p => p.Provider.Id).ToHashSet(StringComparer.Ordinal),
                    Snapshots = cache.LoadAll(registry.Providers.Select(p => p.Provider.Id)),
                    LimitDescriptors = registry.LimitDescriptorsByProvider,
                    Errors = coordinator.Errors,
                    GeneratedAt = DateTimeOffset.UtcNow,
                },
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("api"));
        });
        builder.Services.AddSingleton(sp => new FolderHistorySync(paths.HistoryDirectory, settings, sp.GetRequiredService<IClock>()));
        builder.Services.AddHostedService<RefreshLoop>();
        builder.Services.AddHostedService<ApiHostedService>();

        var host = builder.Build();
        return new UsageApplication
        {
            Host = host,
            Paths = paths,
            Settings = settings,
            Enablement = host.Services.GetRequiredService<ProviderEnablement>(),
            Registry = host.Services.GetRequiredService<WidgetRegistry>(),
            Refresh = host.Services.GetRequiredService<IRefreshCoordinator>(),
            Cache = host.Services.GetRequiredService<SnapshotCache>(),
            Api = host.Services.GetRequiredService<LocalUsageServer>(),
        };
    }

    public async Task SeedEnablementAsync(CancellationToken cancellationToken)
    {
        Enablement.RegisterKnown(Registry.Providers.Select(p => p.Provider.Id));
        if (Settings.Get<string[]>(AppSettings.EnabledProvidersKey) is not null)
        {
            return;
        }

        var detected = new List<string>();
        foreach (var provider in Registry.Providers)
        {
            if (await provider.HasLocalCredentialsAsync(cancellationToken).ConfigureAwait(false))
            {
                detected.Add(provider.Provider.Id);
            }
        }

        if (detected.Count > 0)
        {
            Enablement.SeedEnabled(detected);
        }
        else
        {
            Enablement.SeedEnabled([]);
        }
    }

    public async ValueTask DisposeAsync() => await Host.StopAsync().ConfigureAwait(false);
}

file sealed class RefreshLoop(IRefreshCoordinator refresh) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await refresh.RefreshAllAsync(force: false, stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(RefreshSetting.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await refresh.RefreshAllAsync(force: false, stoppingToken).ConfigureAwait(false);
        }
    }
}

file sealed class ApiHostedService(LocalUsageServer server, ISettingsStore settings) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        settings.Get<bool?>(AppSettings.LocalApiEnabledKey) == false
            ? Task.CompletedTask
            : server.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken) => await server.DisposeAsync().ConfigureAwait(false);
}
