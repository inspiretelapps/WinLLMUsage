using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Refresh;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Infrastructure.Cache;

namespace WinLLMUsage.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IRefreshCoordinator _refresh;
    private readonly WidgetRegistry _registry;
    private readonly ProviderEnablement _enablement;
    private readonly SnapshotCache _cache;

    public DashboardViewModel(
        IRefreshCoordinator refresh,
        WidgetRegistry registry,
        ProviderEnablement enablement,
        SnapshotCache cache,
        ISettingsStore settings)
    {
        _refresh = refresh;
        _registry = registry;
        _enablement = enablement;
        _cache = cache;
        Settings = settings;
        PrivacyMode = settings.Get<bool?>(AppSettings.PrivacyModeKey) == true;
    }

    public ISettingsStore Settings { get; }
    public ProviderEnablement Enablement => _enablement;
    public WidgetRegistry Registry => _registry;

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
        var snapshots = _cache.LoadAll(_registry.Providers.Select(p => p.Provider.Id));
        Cards = _registry.Providers
            .Where(p => _enablement.IsEnabled(p.Provider.Id))
            .Select(p => new ProviderCardViewModel(p.Provider, snapshots.GetValueOrDefault(p.Provider.Id), PrivacyMode))
            .ToArray();
        OnPropertyChanged(nameof(Cards));
    }

    public void SetPrivacy(bool enabled)
    {
        PrivacyMode = enabled;
        Settings.Set(AppSettings.PrivacyModeKey, enabled);
        Rebuild();
    }
}

public sealed class ProviderCardViewModel
{
    public ProviderCardViewModel(Provider provider, ProviderSnapshot? snapshot, bool privacy)
    {
        Title = provider.DisplayName;
        Plan = privacy ? "•••" : snapshot?.Plan ?? "";
        Warning = snapshot?.Warning ?? "";
        Rows = (snapshot?.Lines ?? []).Select(line => new MetricRowViewModel(line, privacy)).ToArray();
    }

    public string Title { get; }
    public string Plan { get; }
    public string Warning { get; }
    public IReadOnlyList<MetricRowViewModel> Rows { get; }
}

public sealed class MetricRowViewModel
{
    public MetricRowViewModel(MetricLine line, bool privacy)
    {
        Label = line.Label;
        Value = privacy ? "•••" : Format(line);
    }

    public string Label { get; }
    public string Value { get; }

    private static string Format(MetricLine line) => line switch
    {
        MetricLine.ProgressLine p => $"{MetricFormatter.Number(p.Used, p.Format.MetricKind, FormatStyle.Row)} / {MetricFormatter.Number(p.Limit, p.Format.MetricKind, FormatStyle.Row)}",
        MetricLine.ValuesLine v => MetricFormatter.LegacyCombined(v.Items),
        MetricLine.BadgeLine b => b.BadgeText,
        MetricLine.TextLine t => t.Value,
        MetricLine.ChartLine => "trend",
        _ => "",
    };
}
