using System.Collections.Concurrent;
using System.Text.Json;
using WinLLMUsage.Core.Contracts;

namespace WinLLMUsage.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public JsonSettingsStore(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    _values[property.Name] = property.Value.Clone();
                }
            }
            catch (JsonException)
            {
            }
        }
    }

    public T? Get<T>(string key)
    {
        if (!_values.TryGetValue(key, out var element))
        {
            return default;
        }

        try
        {
            return element.Deserialize<T>();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Set<T>(string key, T value)
    {
        _values[key] = JsonSerializer.SerializeToElement(value);
        Persist();
    }

    public void Remove(string key)
    {
        _values.TryRemove(key, out _);
        Persist();
    }

    public bool Contains(string key) => _values.ContainsKey(key);

    private void Persist()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var obj = new Dictionary<string, JsonElement>(_values, StringComparer.Ordinal);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, _path, overwrite: true);
        }
    }
}
