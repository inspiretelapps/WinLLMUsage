namespace WinLLMUsage.Core;

/// <summary>Product identity for WinLLMUsage. Independent of upstream OpenUsage branding.</summary>
public static class AppInfo
{
    public const string ProductName = "WinLLMUsage";
    public const string Version = "0.1.0-dev";
    public const string PackageId = "com.winllmusage.app";
    public const string DataDirectoryName = "WinLLMUsage";
    public const string UpstreamOrigin = "OpenUsage v0.7.11 (https://github.com/robinebers/openusage)";
    public const string UpstreamCommit = "753e2fe4bc82a011567d16d8deb88176b58ed24e";
    public const int LocalApiPort = 6736;
    public const int LocalApiMaxConnections = 16;
    public const string LimitsSchema = "openusage.limits.v1";
    public const string HistorySchemaV1 = "openusage.history.v1";
    public const string HistorySchemaV2 = "openusage.history.v2";
    public const string SettingsSchemaKey = "openusage.settings.schemaVersion";
    public const int SettingsSchemaCurrent = 3;
}
