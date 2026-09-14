#if WINDOWS_APP
using System.Windows;
using WinLLMUsage.App.ViewModels;
using WinLLMUsage.Host;
using WinLLMUsage.Windows;
using Forms = System.Windows.Forms;

namespace WinLLMUsage.App.WindowsUi;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly DashboardWindow _dashboard;

    public TrayController(DashboardWindow dashboard, UsageApplication usage)
    {
        _dashboard = dashboard;
        _icon = new Forms.NotifyIcon
        {
            Text = "WinLLMUsage",
            Visible = true,
            Icon = System.Drawing.SystemIcons.Application,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                dashboard.ShowNearTray();
            }
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => dashboard.ShowNearTray());
        menu.Items.Add("Refresh", null, async (_, _) =>
        {
            if (dashboard.DataContext is DashboardViewModel vm)
            {
                await vm.RefreshCommand.ExecuteAsync(null);
            }
        });
        menu.Items.Add("Privacy Mode", null, (_, _) =>
        {
            if (dashboard.DataContext is DashboardViewModel vm)
            {
                vm.SetPrivacy(!vm.PrivacyMode);
            }
        });
        menu.Items.Add("Settings", null, (_, _) => new SettingsWindow((DashboardViewModel)dashboard.DataContext).ShowDialog());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _icon.ContextMenuStrip = menu;
        _ = usage;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
#endif
