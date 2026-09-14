#if WINDOWS_APP
using System.Windows;
using System.Windows.Input;
using WinLLMUsage.App.ViewModels;
using WinLLMUsage.Windows;

namespace WinLLMUsage.App.WindowsUi;

public partial class DashboardWindow : Window
{
    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Rebuild();
    }

    public void ShowNearTray()
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Max(work.Left, work.Right - Width - 12);
        Top = Math.Max(work.Top, work.Bottom - ActualHeight - 12);
        if (ActualHeight < 10)
        {
            Top = work.Bottom - 400;
        }

        Show();
        Activate();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => new SettingsWindow((DashboardViewModel)DataContext).ShowDialog();

    private void Customize_Click(object sender, RoutedEventArgs e) => new CustomizeWindow((DashboardViewModel)DataContext).ShowDialog();

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
        else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DataContext is DashboardViewModel vm)
            {
                await vm.RefreshCommand.ExecuteAsync(null);
            }
        }
        else if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Settings_Click(sender, e);
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();
}
#endif
