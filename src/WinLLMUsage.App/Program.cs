using Microsoft.Extensions.Hosting;
using WinLLMUsage.Core;
using WinLLMUsage.Host;
using WinLLMUsage.Windows;

namespace WinLLMUsage.App;

public static class Program
{
#if WINDOWS_APP
    [STAThread]
#endif
    public static int Main(string[] args)
    {
#if WINDOWS_APP
        Velopack.VelopackApp.Build().Run();
#endif
        using var instance = new SingleInstanceGuard(@"Local\WinLLMUsage-" + Environment.UserName);
        if (!instance.CreatedNew)
        {
            instance.SignalExisting();
            return 0;
        }

#if WINDOWS_APP
        return DesktopProgram.Run(args, instance);
#else
        return HeadlessProgram.RunAsync(args).GetAwaiter().GetResult();
#endif
    }
}

internal static class HeadlessProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var app = UsageApplication.Create(cliCacheFreshness: false, args);
        await using (app.ConfigureAwait(false))
        {
            await app.SeedEnablementAsync(CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version} running. Local API: http://127.0.0.1:{AppInfo.LocalApiPort}");
            await app.Host.RunAsync().ConfigureAwait(false);
        }

        return 0;
    }
}
