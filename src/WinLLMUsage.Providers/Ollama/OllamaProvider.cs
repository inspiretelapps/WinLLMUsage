using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;

namespace WinLLMUsage.Providers.Ollama;

public sealed class OllamaProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public OllamaProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Ollama();
        WidgetDescriptors = KnownProviders.OllamaDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var keyPath = Path.Combine(_paths.UserProfile, ".ollama", "id_ed25519");
        if (!File.Exists(keyPath))
        {
            return ProviderSnapshot.Error(Provider, "Ollama signing key not found (~/.ollama/id_ed25519).", ErrorCategory.NotLoggedIn);
        }

        Ed25519PrivateKeyParameters privateKey;
        try
        {
            using var reader = File.OpenText(keyPath);
            var pem = new PemReader(reader).ReadObject();
            privateKey = pem as Ed25519PrivateKeyParameters
                         ?? throw new InvalidDataException("Not an Ed25519 OpenSSH key.");
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Could not parse the Ollama Ed25519 key.", ErrorCategory.Decoding);
        }

        var usage = await SignedAsync(privateKey, "GET", "/api/usage", cancellationToken).ConfigureAwait(false);
        if (usage.Status is 401 or 403)
        {
            return ProviderSnapshot.Error(Provider, "Ollama Cloud is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        if (!usage.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Ollama usage request failed ({usage.Status}).", ProviderAuthRetry.Classify(usage.Status));
        }

        IReadOnlyList<MetricLine> lines;
        try
        {
            lines = OllamaUsageMapper.MapUsage(usage.Text);
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Ollama usage response could not be mapped.", ErrorCategory.Decoding);
        }

        string? plan = null;
        string? warning = null;
        try
        {
            var account = await SignedAsync(privateKey, "POST", "/api/me", cancellationToken).ConfigureAwait(false);
            if (account.IsSuccess)
            {
                plan = OllamaUsageMapper.PlanName(account.Text);
            }
            else
            {
                warning = "Couldn't read your Ollama plan. Usage below is still up to date.";
            }
        }
        catch (Exception)
        {
            warning = "Couldn't read your Ollama plan. Usage below is still up to date.";
        }

        return ProviderSnapshot.Make(Provider, plan, lines, _clock.Now, warning: warning);
    }

    private async Task<HttpResponse> SignedAsync(Ed25519PrivateKeyParameters key, string method, string path, CancellationToken cancellationToken)
    {
        var requestUri = $"{path}?ts={_clock.Now.ToUnixTimeSeconds()}";
        var authorization = OllamaRequestSigner.Authorization(key, method, requestUri);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = authorization,
            ["Accept"] = "application/json",
        };
        var url = "https://ollama.com" + requestUri;
        return method == "POST"
            ? await _http.SendAsync(JsonRequest.PostJson(url, "{}", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false)
            : await _http.SendAsync(JsonRequest.Get(url, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
    }
}
