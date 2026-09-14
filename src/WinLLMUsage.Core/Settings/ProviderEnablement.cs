using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Layout;

namespace WinLLMUsage.Core.Settings;

public sealed class ProviderEnablement
{
    private readonly ISettingsStore _store;
    private HashSet<string>? _enabledIds;
    private HashSet<string> _disabledIds;
    private HashSet<string> _knownIds;

    public ProviderEnablement(ISettingsStore store)
    {
        _store = store;
        var enabled = store.Get<string[]>(AppSettings.EnabledProvidersKey);
        if (enabled is not null)
        {
            _enabledIds = [..enabled];
            _disabledIds = [];
        }
        else
        {
            _enabledIds = null;
            _disabledIds = [..(store.Get<string[]>(AppSettings.DisabledProvidersKey) ?? [])];
        }

        _knownIds = [..(store.Get<string[]>(AppSettings.KnownProvidersKey) ?? [])];
    }

    public IReadOnlySet<string> KnownIds => _knownIds;

    public bool IsEnabled(string id)
    {
        if (_enabledIds is not null)
        {
            return _enabledIds.Contains(id);
        }

        return !_disabledIds.Contains(id);
    }

    public bool SetEnabled(string id, bool enabled)
    {
        if (_enabledIds is not null)
        {
            var next = new HashSet<string>(_enabledIds, StringComparer.Ordinal);
            if (enabled)
            {
                next.Add(id);
            }
            else
            {
                next.Remove(id);
            }

            if (next.SetEquals(_enabledIds))
            {
                return false;
            }

            _enabledIds = next;
            Persist();
            return true;
        }

        var nextDisabled = new HashSet<string>(_disabledIds, StringComparer.Ordinal);
        if (enabled)
        {
            nextDisabled.Remove(id);
        }
        else
        {
            nextDisabled.Add(id);
        }

        if (nextDisabled.SetEquals(_disabledIds))
        {
            return false;
        }

        _disabledIds = nextDisabled;
        Persist();
        return true;
    }

    public void SeedEnabled(IEnumerable<string> ids)
    {
        _enabledIds = new HashSet<string>(ids, StringComparer.Ordinal);
        Persist();
    }

    public void RegisterKnown(IEnumerable<string> ids)
    {
        var changed = false;
        foreach (var id in ids)
        {
            changed |= _knownIds.Add(id);
        }

        if (changed)
        {
            Persist();
        }
    }

    public IReadOnlySet<string> NeverSeen(IEnumerable<string> registryIds) =>
        registryIds.Where(id => !_knownIds.Contains(id)).ToHashSet(StringComparer.Ordinal);

    public void ResetToDetected(IEnumerable<string> detectedIds)
    {
        _enabledIds = new HashSet<string>(detectedIds, StringComparer.Ordinal);
        if (_enabledIds.Count == 0)
        {
            _enabledIds.UnionWith(["claude", "codex", "cursor"]);
        }

        Persist();
    }

    private void Persist()
    {
        if (_enabledIds is not null)
        {
            _store.Set(AppSettings.EnabledProvidersKey, _enabledIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        else
        {
            _store.Set(AppSettings.DisabledProvidersKey, _disabledIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }

        _store.Set(AppSettings.KnownProvidersKey, _knownIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        _ = DefaultLayout.V2KnownProviders;
    }
}
