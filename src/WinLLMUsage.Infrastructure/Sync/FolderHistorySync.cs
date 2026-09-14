using System.Text.Json;
using WinLLMUsage.Core;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.Infrastructure.Sync;

public sealed class FolderHistorySync
{
    private readonly string _directory;
    private readonly ISettingsStore _settings;
    private readonly IClock _clock;

    public FolderHistorySync(string directory, ISettingsStore settings, IClock clock)
    {
        _directory = directory;
        _settings = settings;
        _clock = clock;
        Directory.CreateDirectory(directory);
    }

    public string DeviceId
    {
        get
        {
            var existing = _settings.Get<string>(AppSettings.DeviceIdKey);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var id = Guid.NewGuid().ToString("D").ToLowerInvariant();
            _settings.Set(AppSettings.DeviceIdKey, id);
            return id;
        }
    }

    public async Task PublishAsync(UsageHistoryDocument document, CancellationToken cancellationToken)
    {
        document.DeviceId = DeviceId;
        document.UpdatedAt = _clock.Now;
        var path = Path.Combine(_directory, DeviceId + ".json");
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(document), cancellationToken).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    public async Task<IReadOnlyList<UsageHistoryDocument>> ListPeersAsync(CancellationToken cancellationToken)
    {
        var list = new List<UsageHistoryDocument>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var document = JsonSerializer.Deserialize<UsageHistoryDocument>(json);
                if (document is not null)
                {
                    document.Validate();
                    list.Add(document);
                }
            }
            catch (Exception)
            {
            }
        }

        return list;
    }

    public Task DeleteLocalAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_directory, DeviceId + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
