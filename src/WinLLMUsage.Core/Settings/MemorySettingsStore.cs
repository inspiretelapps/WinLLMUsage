using System.Collections.Concurrent;
using WinLLMUsage.Core.Contracts;

namespace WinLLMUsage.Core.Settings;

public sealed class MemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);

    public T? Get<T>(string key)
    {
        if (!_values.TryGetValue(key, out var value) || value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        return default;
    }

    public void Set<T>(string key, T value) => _values[key] = value;

    public void Remove(string key) => _values.TryRemove(key, out _);

    public bool Contains(string key) => _values.ContainsKey(key);
}
