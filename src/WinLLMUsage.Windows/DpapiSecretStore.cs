using System.Security.Cryptography;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Infrastructure.Secrets;

namespace WinLLMUsage.Windows;

public sealed class DpapiSecretStore : ISecretStore
{
    private readonly FileSecretStore _inner;

    public DpapiSecretStore(string directory)
    {
        _inner = new FileSecretStore(
            directory,
            bytes => OperatingSystem.IsWindows()
                ? ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)
                : bytes,
            bytes => OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser)
                : bytes);
    }

    public Task<byte[]?> UnprotectAsync(string name, CancellationToken cancellationToken) =>
        _inner.UnprotectAsync(name, cancellationToken);

    public Task ProtectAsync(string name, byte[] plaintext, CancellationToken cancellationToken) =>
        _inner.ProtectAsync(name, plaintext, cancellationToken);

    public Task DeleteAsync(string name, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(name, cancellationToken);
}
