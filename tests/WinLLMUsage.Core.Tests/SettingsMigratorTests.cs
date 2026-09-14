using WinLLMUsage.Core;
using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.Core.Tests;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void FreshStoreStampsSchemaThreeWithoutMigrating()
    {
        var store = new MemorySettingsStore();
        SettingsMigrator.Migrate(store);
        Assert.Equal(3, store.Get<int?>(AppInfo.SettingsSchemaKey));
        Assert.False(store.Contains(AppSettings.EnabledProvidersKey));
    }

    [Fact]
    public void V2ConvertsDisabledList()
    {
        var store = new MemorySettingsStore();
        store.Set(AppSettings.DisabledProvidersKey, new[] { "grok" });
        SettingsMigrator.Migrate(store);
        var enabled = store.Get<string[]>(AppSettings.EnabledProvidersKey)!;
        Assert.DoesNotContain("grok", enabled);
        Assert.Contains("claude", enabled);
        Assert.Equal(3, store.Get<int?>(AppInfo.SettingsSchemaKey));
    }
}
