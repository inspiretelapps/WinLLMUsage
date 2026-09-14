using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinLLMUsage.Core;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Infrastructure.Api;
using WinLLMUsage.Infrastructure.Cache;
using WinLLMUsage.Infrastructure.Http;
using WinLLMUsage.Infrastructure.Logging;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Infrastructure.Refresh;
using WinLLMUsage.Infrastructure.Secrets;
using WinLLMUsage.Infrastructure.Settings;
using WinLLMUsage.Providers.Catalog;
using WinLLMUsage.Windows;

using var mutex = new Mutex(true, @"Local\WinLLMUsage-" + Environment.UserName, out var created);
if (!created)
{
    Console.Error.WriteLine("WinLLMUsage is already running.");
    return 0;
}

var paths = new AppPaths();
paths.EnsureCreated();
var settings = new JsonSettingsStore(paths.SettingsFile);
SettingsMigrator.Migrate(settings);
ISecretStore secrets = OperatingSystem.IsWindows()
    ? new DpapiSecretStore(paths.SecretsDirectory)
    : new FileSecretStore(paths.SecretsDirectory);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new FileLoggerProvider(paths.LogFile));
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<ISettingsStore>(settings);
builder.Services.AddSingleton(secrets);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(sp => new HttpTransport(settings.Get<ProxySettings>("proxy") ?? new ProxySettings()));
builder.Services.AddSingleton<IHttpTransport>(sp => sp.GetRequiredService<HttpTransport>());
builder.Services.AddSingleton(sp => new WidgetRegistry(ProviderCatalog.Create(
    sp.GetRequiredService<IHttpTransport>(),
    secrets,
    paths,
    sp.GetRequiredService<IClock>(),
    settings)));
builder.Services.AddSingleton(_ => new ProviderEnablement(settings));
builder.Services.AddSingleton(sp => new SnapshotCache(paths.SnapshotsFile, sp.GetRequiredService<IClock>(), allowsPersistedFreshness: false));
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
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("api");
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
        logger);
});
builder.Services.AddHostedService<RefreshLoop>();
builder.Services.AddHostedService<ApiHostedService>();

using var host = builder.Build();
var enablement = host.Services.GetRequiredService<ProviderEnablement>();
var registry = host.Services.GetRequiredService<WidgetRegistry>();
enablement.RegisterKnown(registry.Providers.Select(p => p.Provider.Id));
if (settings.Get<string[]>(AppSettings.EnabledProvidersKey) is null)
{
    var detected = new List<string>();
    foreach (var provider in registry.Providers)
    {
        if (await provider.HasLocalCredentialsAsync(CancellationToken.None).ConfigureAwait(false))
        {
            detected.Add(provider.Provider.Id);
        }
    }

    enablement.ResetToDetected(detected);
}

Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version} running. Local API: http://127.0.0.1:{AppInfo.LocalApiPort}");
await host.RunAsync().ConfigureAwait(false);
return 0;

file sealed class RefreshLoop(IRefreshCoordinator refresh) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await refresh.RefreshAllAsync(force: false, stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(WinLLMUsage.Core.Time.RefreshSetting.Interval);
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
