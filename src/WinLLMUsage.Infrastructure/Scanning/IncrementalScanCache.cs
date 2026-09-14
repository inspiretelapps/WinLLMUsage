using System.Text.Json;

namespace WinLLMUsage.Infrastructure.Scanning;

public sealed class IncrementalScanCache
{
    private readonly string _directory;

    public IncrementalScanCache(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public bool IsUnchanged(string path, long length, DateTimeOffset lastWrite)
    {
        var record = Load(path);
        return record is not null && record.Length == length && record.LastWrite == lastWrite;
    }

    public void Remember(string path, long length, DateTimeOffset lastWrite)
    {
        var file = IndexPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(new ScanRecord(length, lastWrite, 1)));
    }

    public void PruneOlderThan(TimeSpan age)
    {
        var cutoff = DateTimeOffset.UtcNow - age;
        if (!Directory.Exists(_directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json", SearchOption.AllDirectories))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff.UtcDateTime)
            {
                File.Delete(file);
            }
        }
    }

    private ScanRecord? Load(string path)
    {
        var file = IndexPath(path);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanRecord>(File.ReadAllText(file));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string IndexPath(string path)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16];
        return Path.Combine(_directory, hash + ".json");
    }

    private sealed record ScanRecord(long Length, DateTimeOffset LastWrite, int Version);
}
