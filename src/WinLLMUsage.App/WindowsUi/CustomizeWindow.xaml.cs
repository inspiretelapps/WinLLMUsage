#if WINDOWS_APP
using System.Windows;
using System.Windows.Controls;
using WinLLMUsage.App.ViewModels;

namespace WinLLMUsage.App.WindowsUi;

public partial class CustomizeWindow : Window
{
    private readonly DashboardViewModel _vm;

    public CustomizeWindow(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        foreach (var provider in vm.Registry.Providers)
        {
            var box = new CheckBox
            {
                Content = provider.Provider.DisplayName,
                IsChecked = vm.Enablement.IsEnabled(provider.Provider.Id),
                Tag = provider.Provider.Id,
                Foreground = Foreground,
                Margin = new Thickness(0, 4, 0, 4),
            };
            box.Checked += Toggle;
            box.Unchecked += Toggle;
            Providers.Items.Add(box);
        }
    }

    private void Toggle(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: string id } box)
        {
            _vm.Enablement.SetEnabled(id, box.IsChecked == true);
            _vm.Rebuild();
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
#endif
