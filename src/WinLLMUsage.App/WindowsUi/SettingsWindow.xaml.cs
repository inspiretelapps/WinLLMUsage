#if WINDOWS_APP
using System.Windows;
using WinLLMUsage.App.ViewModels;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Windows;

namespace WinLLMUsage.App.WindowsUi;

public partial class SettingsWindow : Window
{
    private readonly DashboardViewModel _vm;

    public SettingsWindow(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Privacy.IsChecked = vm.PrivacyMode;
        Api.IsChecked = vm.Settings.Get<bool?>(AppSettings.LocalApiEnabledKey) != false;
        Startup.IsChecked = StartupRegistration.IsEnabled();
        Pacing.IsChecked = vm.Settings.Get<bool?>("alwaysShowPacing") == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetPrivacy(Privacy.IsChecked == true);
        _vm.Settings.Set(AppSettings.LocalApiEnabledKey, Api.IsChecked == true);
        _vm.Settings.Set("alwaysShowPacing", Pacing.IsChecked == true);
        var exe = Environment.ProcessPath ?? "";
        StartupRegistration.SetEnabled(Startup.IsChecked == true, exe);
        Close();
    }
}
#endif
