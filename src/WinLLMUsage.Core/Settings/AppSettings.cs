using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Settings;

public enum AppearanceSetting
{
    System,
    Light,
    Dark,
}

public enum DensitySetting
{
    Regular,
    Compact,
}

public enum TimeFormatSetting
{
    Auto,
    TwelveHour,
    TwentyFourHour,
}

public enum LogLevelSetting
{
    Error,
    Warn,
    Info,
    Debug,
}

public enum TotalSpendPeriod
{
    Today,
    Yesterday,
    Last30,
}

public enum TotalSpendMetric
{
    Cost,
    Tokens,
    CostPerMtok,
}

public sealed class AppSettings
{
    public const string EnabledProvidersKey = "openusage.enabledProviders.v1";
    public const string DisabledProvidersKey = "openusage.disabledProviders.v1";
    public const string KnownProvidersKey = "openusage.knownProviders.v1";
    public const string LayoutKey = "openusage.layout.v1";
    public const string SnapshotCacheKey = "openusage.providerSnapshots.v9";
    public const string DeviceIdKey = "openusage.icloudSync.deviceID.v1";
    public const string SyncEnabledKey = "openusage.icloudSync.enabled.v1";
    public const string CodexFallbackModelKey = "openusage.codex.fallbackModel";
    public const string LocalApiEnabledKey = "winllmusage.localApi.enabled";
    public const string PrivacyModeKey = "winllmusage.privacyMode";
    public const string FloatingStripEnabledKey = "winllmusage.floatingStrip.enabled";
    public const string OpenRouterKeyName = "openrouter.apiKey";
    public const string ZaiKeyName = "zai.apiKey";

    public WidgetDisplayMode MeterStyle { get; set; } = WidgetDisplayMode.Remaining;
    public ResetDisplayMode ResetDisplayMode { get; set; } = ResetDisplayMode.Relative;
    public bool AlwaysShowPacing { get; set; }
    public AppearanceSetting Appearance { get; set; } = AppearanceSetting.System;
    public DensitySetting Density { get; set; } = DensitySetting.Regular;
    public TimeFormatSetting TimeFormat { get; set; } = TimeFormatSetting.Auto;
    public LogLevelSetting LogLevel { get; set; } = LogLevelSetting.Info;
    public bool ReduceAnimations { get; set; }
    public bool IncreaseTransparency { get; set; }
    public bool PrivacyMode { get; set; }
    public bool ShowTotalSpend { get; set; } = true;
    public TotalSpendPeriod TotalSpendPeriod { get; set; } = TotalSpendPeriod.Today;
    public TotalSpendMetric TotalSpendMetric { get; set; } = TotalSpendMetric.Cost;
    public bool NotifyUnderTenPercent { get; set; }
    public bool NotifyHealthyToClose { get; set; }
    public bool NotifyCloseToRunningOut { get; set; }
    public bool SyncEnabled { get; set; }
    public string? SyncFolder { get; set; }
    public bool LocalApiEnabled { get; set; } = true;
    public bool LaunchAtLogin { get; set; }
    public string? GlobalHotkey { get; set; }
    public MenuBarStyle MenuBarStyle { get; set; } = MenuBarStyle.Text;
    public bool FloatingStripEnabled { get; set; }
    public string CodexFallbackModel { get; set; } = "";
    public ProxySettings Proxy { get; set; } = new();
}

public sealed class ProxySettings
{
    public bool Enabled { get; set; }
    public string? Url { get; set; }
}

public sealed class PaceNotificationToggles
{
    public bool UnderTenPercent { get; init; }
    public bool HealthyToClose { get; init; }
    public bool CloseToRunningOut { get; init; }
}
