#if WINDOWS
using System.Windows;
using WinLLMUsage.App.ViewModels;

namespace WinLLMUsage.App.WindowsUi;

public partial class DashboardWindow : Window
{
    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Settings are stored in %LOCALAPPDATA%\\WinLLMUsage\\settings.json", "Settings");
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
#endif
