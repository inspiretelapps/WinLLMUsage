using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

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
        Task.FromResult(false); // Ollama is explicitly opt-in.

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
                         ?? (pem as Ed25519PrivateKeyParameters)
                         ?? throw new InvalidDataException("Not an Ed25519 OpenSSH key.");
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Could not parse the Ollama Ed25519 key.", ErrorCategory.Decoding);
        }

        var usage = await SignedGetAsync(privateKey, "https://ollama.com/api/usage", cancellationToken).ConfigureAwait(false);
        if (!usage.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Ollama usage request failed ({usage.Status}).", ProviderAuthRetry.Classify(usage.Status));
        }

        using var doc = JsonDocument.Parse(usage.Body);
        var root = doc.RootElement;
        var lines = new List<MetricLine>();
        AddPercent(root, "session", "Session", lines);
        AddPercent(root, "weekly", "Weekly", lines);
        var spent = root.GetDouble("last_4_weeks", "last4Weeks", "additional_charges") ?? 0;
        lines.Add(MetricLine.Values("Last 4 Weeks", [new MetricValue(spent, MetricKind.Dollars)]));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, root.GetString("plan"), lines, _clock.Now);
    }

    private static void AddPercent(JsonElement root, string key, string label, List<MetricLine> lines)
    {
        var window = root.GetObject(key);
        if (window is null)
        {
            return;
        }

        var used = window.Value.GetDouble("used", "utilization", "percent") ?? 0;
        lines.Add(MetricLine.Progress(label, used, 100, ProgressFormat.Percent));
    }

    private async Task<HttpResponse> SignedGetAsync(Ed25519PrivateKeyParameters key, string url, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);
        var timestamp = _clock.Now.ToUnixTimeSeconds().ToString();
        var payload = Encoding.UTF8.GetBytes($"{timestamp},{uri.PathAndQuery}");
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(payload, 0, payload.Length);
        var signature = Convert.ToBase64String(signer.GenerateSignature());
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + timestamp + ":" + signature,
            ["Accept"] = "application/json",
        };
        return await _http.SendAsync(JsonRequest.Get(url, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
    }
}
