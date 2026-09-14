#if WINDOWS_APP
using System.Windows;
using WinLLMUsage.App.ViewModels;
using WinLLMUsage.Host;
using WinLLMUsage.Windows;

namespace WinLLMUsage.App;

internal static class DesktopProgram
{
    public static int Run(string[] args, SingleInstanceGuard instance)
    {
        var usage = UsageApplication.Create(cliCacheFreshness: false, args);
        usage.SeedEnablementAsync(CancellationToken.None).GetAwaiter().GetResult();
        usage.Host.Start();

        var wpf = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        var dashboard = new WindowsUi.DashboardWindow(new DashboardViewModel(usage.Refresh, usage.Registry, usage.Enablement, usage.Cache, usage.Settings));
        var tray = new WindowsUi.TrayController(dashboard, usage);
        instance.Activated += () => dashboard.Dispatcher.Invoke(dashboard.ShowNearTray);
        instance.StartListening();
        dashboard.ShowNearTray();
        wpf.Run();
        tray.Dispose();
        usage.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return 0;
    }
}
#endif
