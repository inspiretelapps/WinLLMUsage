using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IRefreshCoordinator _refresh;
    private readonly WidgetRegistry _registry;
    private readonly ProviderEnablement _enablement;

    public DashboardViewModel(IRefreshCoordinator refresh, WidgetRegistry registry, ProviderEnablement enablement)
    {
        _refresh = refresh;
        _registry = registry;
        _enablement = enablement;
    }

    [ObservableProperty]
    private bool privacyMode;

    [ObservableProperty]
    private string status = "Ready";

    public IReadOnlyList<ProviderCardViewModel> Cards { get; private set; } = [];

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Status = "Refreshing…";
        await _refresh.RefreshAllAsync(force: true, CancellationToken.None).ConfigureAwait(true);
        Rebuild();
        Status = "Updated";
    }

    public void Rebuild()
    {
        var snapshots = _registry.Providers
            .Where(p => _enablement.IsEnabled(p.Provider.Id))
            .Select(p => new ProviderCardViewModel(p.Provider, p.WidgetDescriptors))
            .ToArray();
        Cards = snapshots;
        OnPropertyChanged(nameof(Cards));
    }
}

public sealed class ProviderCardViewModel
{
    public ProviderCardViewModel(Provider provider, IReadOnlyList<WidgetDescriptor> descriptors)
    {
        Provider = provider;
        Descriptors = descriptors;
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> Descriptors { get; }
    public string Title => Provider.DisplayName;
}
