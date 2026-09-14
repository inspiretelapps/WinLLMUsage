using System.Text.Json;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Infrastructure.Cache;

public sealed class SnapshotCache : ISnapshotRepository
{
    private readonly string _path;
    private readonly IClock _clock;
    private readonly bool _allowsPersistedFreshness;
    private readonly object _gate = new();
    private Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private bool _loaded;

    public SnapshotCache(string path, IClock clock, bool allowsPersistedFreshness)
    {
        _path = path;
        _clock = clock;
        _allowsPersistedFreshness = allowsPersistedFreshness;
    }

    public ProviderSnapshot? Load(string providerId)
    {
        EnsureLoaded();
        return _entries.TryGetValue(providerId, out var entry) ? entry.Snapshot : null;
    }

    public IReadOnlyDictionary<string, ProviderSnapshot> LoadAll(IEnumerable<string> providerIds)
    {
        EnsureLoaded();
        var result = new Dictionary<string, ProviderSnapshot>(StringComparer.Ordinal);
        foreach (var id in providerIds)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                result[id] = entry.Snapshot;
            }
        }

        return result;
    }

    public bool IsFresh(string providerId)
    {
        EnsureLoaded();
        if (!_entries.TryGetValue(providerId, out var entry))
        {
            return false;
        }

        if (!_allowsPersistedFreshness && !entry.WrittenThisProcess)
        {
            return false;
        }

        return _clock.Now - entry.Snapshot.RefreshedAt < RefreshSetting.Interval;
    }

    public void Store(ProviderSnapshot snapshot, string? identityKey)
    {
        if (snapshot.IsError)
        {
            return;
        }

        EnsureLoaded();
        lock (_gate)
        {
            _entries[snapshot.ProviderId] = new CacheEntry(snapshot, identityKey, WrittenThisProcess: true);
            Persist();
        }
    }

    public bool HasStaleAccountStamp(string providerId, string? currentIdentityKey)
    {
        EnsureLoaded();
        if (currentIdentityKey is null || !_entries.TryGetValue(providerId, out var entry))
        {
            return false;
        }

        return entry.IdentityKey is not null && !string.Equals(entry.IdentityKey, currentIdentityKey, StringComparison.Ordinal);
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json, JsonDefaults.Cache);
                    if (loaded is not null)
                    {
                        _entries = new Dictionary<string, CacheEntry>(loaded, StringComparer.Ordinal);
                    }
                }
            }
            catch (JsonException)
            {
                _entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
            }

            _loaded = true;
        }
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, JsonDefaults.Cache));
        File.Move(tmp, _path, overwrite: true);
    }

    public sealed record CacheEntry(ProviderSnapshot Snapshot, string? IdentityKey, bool WrittenThisProcess = false);
}
