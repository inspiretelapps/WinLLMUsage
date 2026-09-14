using WinLLMUsage.Core.Contracts;

namespace WinLLMUsage.Infrastructure.Secrets;

/// <summary>
/// Restrictive-file secret store. Windows hosts should wrap this with DPAPI
/// (<c>DpapiSecretStore</c>) before persisting app-owned keys.
/// </summary>
public sealed class FileSecretStore : ISecretStore
{
    private readonly string _directory;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;

    public FileSecretStore(string directory, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null)
    {
        _directory = directory;
        _protect = protect ?? (bytes => bytes);
        _unprotect = unprotect ?? (bytes => bytes);
        Directory.CreateDirectory(directory);
    }

    public Task<byte[]?> UnprotectAsync(string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.FromResult<byte[]?>(_unprotect(File.ReadAllBytes(path)));
    }

    public Task ProtectAsync(string name, byte[] plaintext, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, _protect(plaintext));
        File.Move(tmp, path, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string name) => Path.Combine(_directory, Sanitize(name) + ".bin");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }
}
