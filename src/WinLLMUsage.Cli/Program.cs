using Microsoft.Extensions.Logging;
using WinLLMUsage.Core;
using WinLLMUsage.Core.Cli;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Core.Usage;
using WinLLMUsage.Infrastructure.Cache;
using WinLLMUsage.Infrastructure.Http;
using WinLLMUsage.Infrastructure.Logging;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Infrastructure.Refresh;
using WinLLMUsage.Infrastructure.Secrets;
using WinLLMUsage.Infrastructure.Settings;
using WinLLMUsage.Providers.Catalog;

try
{
    var arguments = CliArgumentParser.Parse(args);
    if (arguments.ShowHelp)
    {
        Console.Out.WriteLine(CliArgumentParser.Help);
        return 0;
    }

    if (arguments.ShowVersion)
    {
        Console.Out.WriteLine($"winllmusage {AppInfo.Version}");
        return 0;
    }

    var paths = new AppPaths();
    paths.EnsureCreated();
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddProvider(new FileLoggerProvider(paths.LogFile));
        builder.SetMinimumLevel(LogLevel.Information);
    });
    var settings = new JsonSettingsStore(paths.SettingsFile);
    SettingsMigrator.Migrate(settings);
    var enablement = new ProviderEnablement(settings);
    var proxy = settings.Get<ProxySettings>("proxy") ?? new ProxySettings();
    using var http = new HttpTransport(proxy);
    ISecretStore secrets = OperatingSystem.IsWindows()
        ? new WinLLMUsage.Windows.DpapiSecretStore(paths.SecretsDirectory)
        : new FileSecretStore(paths.SecretsDirectory);
    var clock = new SystemClock();
    var providers = ProviderCatalog.Create(http, secrets, paths, clock, settings);
    enablement.RegisterKnown(providers.Select(p => p.Provider.Id));
    var registry = new WidgetRegistry(providers);
    var cache = new SnapshotCache(paths.SnapshotsFile, clock, allowsPersistedFreshness: true);
    var coordinator = new RefreshCoordinator(registry, cache, enablement, clock, loggerFactory.CreateLogger("refresh"));
    var reader = new UsageReader(registry, enablement, coordinator, () => coordinator.Snapshots, () => coordinator.Errors, clock);
    var result = await reader.ReadAsync(arguments.ProviderId, arguments.Force, CancellationToken.None).ConfigureAwait(false);
    var stdout = UsageReaderStdout.WithTrailingNewline(result.Data);
    Console.OpenStandardOutput().Write(stdout, 0, stdout.Length);
    if (result.Warnings.Count > 0)
    {
        foreach (var warning in result.Warnings)
        {
            Console.Error.WriteLine($"winllmusage: warning: {warning}");
        }

        return 4;
    }

    return 0;
}
catch (CliUsageException ex)
{
    Console.Error.WriteLine($"winllmusage: {ex.Message}");
    Console.Error.WriteLine("Run 'winllmusage --help' for usage.");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"winllmusage: {SecretRedactor.Redact(ex.Message)}");
    return 4;
}
