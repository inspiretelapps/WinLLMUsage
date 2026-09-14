using Microsoft.Extensions.Logging;
using WinLLMUsage.Core;
using WinLLMUsage.Core.Cli;
using WinLLMUsage.Core.Usage;
using WinLLMUsage.Host;
using WinLLMUsage.Infrastructure.Logging;

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

    var app = UsageApplication.Create(cliCacheFreshness: true);
    await using (app.ConfigureAwait(false))
    {
        await app.SeedEnablementAsync(CancellationToken.None).ConfigureAwait(false);
        var logger = app.Host.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        _ = logger;
        var reader = new UsageReader(
            app.Registry,
            app.Enablement,
            app.Refresh,
            () => app.Cache.LoadAll(app.Registry.Providers.Select(p => p.Provider.Id)),
            () => ((WinLLMUsage.Infrastructure.Refresh.RefreshCoordinator)app.Refresh).Errors,
            new WinLLMUsage.Core.Contracts.SystemClock());
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
