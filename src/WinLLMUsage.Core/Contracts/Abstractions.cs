using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Contracts;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public sealed class FrozenClock : IClock
{
    public FrozenClock(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }
}

public interface IProviderRuntime
{
    Provider Provider { get; }

    IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    string? IdentityKey => null;

    Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken);

    Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken);
}

public interface IPricingService
{
    Pricing.ModelPricing Current { get; }

    double? Estimate(
        string model,
        long inputTokens,
        long outputTokens,
        long cacheReadTokens = 0,
        long cacheWriteTokens = 0,
        double priorityMultiplier = 1);
}

public interface IHttpTransport
{
    Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken);
}

public sealed record HttpRequest(
    HttpMethod Method,
    Uri Url,
    IReadOnlyDictionary<string, string>? Headers = null,
    byte[]? Body = null,
    TimeSpan? Timeout = null,
    bool BypassProxy = false);

public sealed record HttpResponse(int Status, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    public string Text => System.Text.Encoding.UTF8.GetString(Body);

    public bool IsSuccess => Status is >= 200 and < 300;
}

public interface ISecretStore
{
    Task<byte[]?> UnprotectAsync(string name, CancellationToken cancellationToken);

    Task ProtectAsync(string name, byte[] plaintext, CancellationToken cancellationToken);

    Task DeleteAsync(string name, CancellationToken cancellationToken);
}

public interface ISnapshotRepository
{
    ProviderSnapshot? Load(string providerId);

    IReadOnlyDictionary<string, ProviderSnapshot> LoadAll(IEnumerable<string> providerIds);

    void Store(ProviderSnapshot snapshot, string? identityKey);

    bool HasStaleAccountStamp(string providerId, string? currentIdentityKey);
}

public interface IRefreshCoordinator
{
    Task RefreshAllAsync(bool force, CancellationToken cancellationToken);

    Task<ProviderSnapshot> RefreshAsync(string providerId, bool force, CancellationToken cancellationToken);
}

public interface ICredentialSource
{
    Task<CredentialLoadResult> LoadAsync(CancellationToken cancellationToken);
}

public sealed record CredentialLoadResult(
    bool HasUsableCredentials,
    string? IdentityKey = null,
    string? Error = null,
    ErrorCategory? Category = null,
    bool Unsupported = false);

public interface IProviderPaths
{
    string UserProfile { get; }

    string LocalAppData { get; }

    string RoamingAppData { get; }

    string AppDataRoot { get; }

    IReadOnlyList<string> CandidateHomes(string providerFamily);
}

public interface IUsageScanner
{
    Task<ProviderUsageHistory?> ScanAsync(CancellationToken cancellationToken);
}

public interface IHistorySyncTransport
{
    Task PublishAsync(UsageHistoryDocument document, CancellationToken cancellationToken);

    Task<IReadOnlyList<UsageHistoryDocument>> ListPeersAsync(CancellationToken cancellationToken);

    Task DeleteLocalAsync(CancellationToken cancellationToken);
}

public interface ISettingsStore
{
    T? Get<T>(string key);

    void Set<T>(string key, T value);

    void Remove(string key);

    bool Contains(string key);
}

public interface ITrayService
{
    void SetTooltip(string text);

    void SetPrivacyMode(bool enabled);
}

public interface INotificationService
{
    Task<bool> PostAsync(string id, string title, string subtitle, string body, CancellationToken cancellationToken);
}
