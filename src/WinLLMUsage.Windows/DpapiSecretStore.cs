using System.Runtime.Versioning;
using System.Security.Cryptography;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Infrastructure.Secrets;

namespace WinLLMUsage.Windows;

public sealed class DpapiSecretStore : ISecretStore
{
    private readonly FileSecretStore _inner;

    public DpapiSecretStore(string directory)
    {
        _inner = new FileSecretStore(directory, Protect, Unprotect);
    }

    public Task<byte[]?> UnprotectAsync(string name, CancellationToken cancellationToken) =>
        _inner.UnprotectAsync(name, cancellationToken);

    public Task ProtectAsync(string name, byte[] plaintext, CancellationToken cancellationToken) =>
        _inner.ProtectAsync(name, plaintext, cancellationToken);

    public Task DeleteAsync(string name, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(name, cancellationToken);

    private static byte[] Protect(byte[] plaintext)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectWindows(plaintext);
        }

        return plaintext;
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return UnprotectWindows(data);
        }

        return data;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ProtectWindows(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] UnprotectWindows(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
