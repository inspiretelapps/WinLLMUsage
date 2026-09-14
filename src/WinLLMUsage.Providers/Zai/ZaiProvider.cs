using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Zai;

public sealed class ZaiProvider : IProviderRuntime
{
    public const string SubscriptionUrl = "https://api.z.ai/api/biz/subscription/list";
    public const string QuotaUrl = "https://api.z.ai/api/monitor/usage/quota/limit";

    private readonly IHttpTransport _http;
    private readonly ISecretStore _secrets;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public ZaiProvider(IHttpTransport http, ISecretStore secrets, AppPaths paths, IClock clock)
    {
        _http = http;
        _secrets = secrets;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Zai();
        WidgetDescriptors = KnownProviders.ZaiDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public async Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await LoadKeyAsync(cancellationToken).ConfigureAwait(false));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var key = await LoadKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            return ProviderSnapshot.Error(Provider, "Add a Z.ai API key in Customize.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + key,
            ["Accept"] = "application/json",
        };
        var quota = await _http.SendAsync(JsonRequest.Get(QuotaUrl, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (quota.Status is 401 or 403)
        {
            return ProviderSnapshot.Error(Provider, "Z.ai API key is invalid.", ErrorCategory.AuthInvalid);
        }

        if (!quota.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Z.ai quota request failed ({quota.Status}).", ProviderAuthRetry.Classify(quota.Status));
        }

        if (ZaiUsageMapper.IsNoCodingPlan(quota.Text))
        {
            return ProviderSnapshot.Error(Provider, "No Z.ai coding plan is attached to this key.", ErrorCategory.NotAvailable);
        }

        IReadOnlyList<MetricLine> lines;
        try
        {
            lines = ZaiUsageMapper.MapQuota(quota.Text);
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Z.ai quota response could not be mapped.", ErrorCategory.Decoding);
        }

        string? plan = null;
        try
        {
            var subscription = await _http.SendAsync(JsonRequest.Get(SubscriptionUrl, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
            if (subscription.IsSuccess)
            {
                plan = ZaiUsageMapper.PlanName(subscription.Text);
            }
        }
        catch (Exception)
        {
        }

        return ProviderSnapshot.Make(Provider, plan, lines, _clock.Now);
    }

    private async Task<string?> LoadKeyAsync(CancellationToken cancellationToken)
    {
        var saved = await _secrets.UnprotectAsync(AppSettings.ZaiKeyName, cancellationToken).ConfigureAwait(false);
        if (saved is { Length: > 0 })
        {
            return System.Text.Encoding.UTF8.GetString(saved);
        }

        foreach (var path in _paths.CandidateHomes("zai"))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
                var key = doc.RootElement.GetString("apiKey", "api_key", "key");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return key;
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        return Environment.GetEnvironmentVariable("ZAI_API_KEY") ?? Environment.GetEnvironmentVariable("GLM_API_KEY");
    }
}
