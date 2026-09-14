using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Layout;

namespace WinLLMUsage.Core.Settings;

public static class SettingsMigrator
{
    public static void Migrate(ISettingsStore store)
    {
        var version = store.Get<int?>(AppInfo.SettingsSchemaKey);
        var hasAnyKeys = store.Contains(AppSettings.EnabledProvidersKey)
                         || store.Contains(AppSettings.DisabledProvidersKey)
                         || store.Contains(AppSettings.LayoutKey);

        if (version is null && !hasAnyKeys)
        {
            store.Set(AppInfo.SettingsSchemaKey, AppInfo.SettingsSchemaCurrent);
            return;
        }

        var current = version ?? 0;
        if (current < 2)
        {
            MigrateV2(store);
            current = 2;
        }

        if (current < 3)
        {
            MigrateV3(store);
            current = 3;
        }

        store.Set(AppInfo.SettingsSchemaKey, current);
    }

    private static void MigrateV2(ISettingsStore store)
    {
        if (store.Get<string[]>(AppSettings.EnabledProvidersKey) is not null)
        {
            return;
        }

        var disabled = store.Get<string[]>(AppSettings.DisabledProvidersKey) ?? [];
        var enabled = DefaultLayout.V2KnownProviders.Except(disabled).ToArray();
        store.Set(AppSettings.EnabledProvidersKey, enabled);
        if (store.Get<string[]>(AppSettings.KnownProvidersKey) is null)
        {
            store.Set(AppSettings.KnownProvidersKey, DefaultLayout.V2KnownProviders);
        }
    }

    private static void MigrateV3(ISettingsStore store)
    {
        RemapList(store, AppSettings.LayoutKey + ".menuBarPins");
        RemapList(store, AppSettings.LayoutKey + ".expandedMetrics");
        RemapList(store, AppSettings.LayoutKey + ".expandOnEnable");
        RemapList(store, AppSettings.LayoutKey + ".seededDefaults");
    }

    private static void RemapList(ISettingsStore store, string key)
    {
        var ids = store.Get<string[]>(key);
        if (ids is null)
        {
            return;
        }

        var remapped = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var next = DefaultLayout.SchemaV3Remaps.GetValueOrDefault(id, id);
            if (seen.Add(next))
            {
                remapped.Add(next);
            }
        }

        store.Set(key, remapped.ToArray());
    }
}
