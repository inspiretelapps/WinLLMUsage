using System.Diagnostics;
using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;

namespace WinLLMUsage.Providers.Antigravity;

public sealed class AntigravityProvider : IProviderRuntime
{
    public const string LsService = "exa.language_server_pb.LanguageServerService";
    public const string QuotaMethod = "RetrieveUserQuotaSummary";

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public AntigravityProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Antigravity();
        WidgetDescriptors = KnownProviders.AntigravityDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(DiscoverEndpoint() is not null || Directory.Exists(Path.Combine(_paths.UserProfile, ".gemini")));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var endpoint = DiscoverEndpoint();
        if (endpoint is null)
        {
            return ProviderSnapshot.Error(Provider, "Antigravity language server is not running.", ErrorCategory.NotAvailable);
        }

        var url = $"{endpoint.Value.Scheme}://127.0.0.1:{endpoint.Value.Port}/{LsService}/{QuotaMethod}";
        var body = JsonSerializer.Serialize(new { metadata = new { ideName = "antigravity", extensionName = "antigravity", ideVersion = "unknown", locale = "en" } });
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Connect-Protocol-Version"] = "1",
        };
        if (!string.IsNullOrWhiteSpace(endpoint.Value.Csrf))
        {
            headers["x-codeium-csrf-token"] = endpoint.Value.Csrf;
        }

        HttpResponse response;
        try
        {
            response = await _http.SendAsync(JsonRequest.PostJson(url, body, headers, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Could not reach the Antigravity language server.", ErrorCategory.Network);
        }

        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Antigravity quota request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        var lines = AntigravityUsageMapper.ParseQuotaSummary(response.Text);
        if (lines is null)
        {
            return ProviderSnapshot.Error(Provider, "Antigravity quota summary could not be mapped.", ErrorCategory.Decoding);
        }

        MetricLine.AppendNoDataIfNeeded(lines as List<MetricLine> ?? lines.ToList());
        var snapshotLines = lines.Count == 0 ? new List<MetricLine> { MetricLine.NoUsageData } : lines.ToList();
        return ProviderSnapshot.Make(Provider, null, snapshotLines, _clock.Now);
    }

    private (string Scheme, int Port, string? Csrf)? DiscoverEndpoint()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!process.ProcessName.Contains("language_server", StringComparison.OrdinalIgnoreCase)
                    && !process.ProcessName.Contains("antigravity", StringComparison.OrdinalIgnoreCase)
                    && !process.ProcessName.Contains("agy", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var args = process.StartInfo.Arguments;
                var port = ParseArg(args, "--port") ?? ParseArg(Environment.CommandLine, "--port");
                var csrf = ParseNamed(args, "--csrf_token") ?? ParseNamed(args, "--csrf");
                if (port is { } p)
                {
                    return ("https", p, csrf);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static int? ParseArg(string? text, string name)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var marker = name + "=";
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = text[(index + marker.Length)..].Split(' ', 2)[0];
        return int.TryParse(rest, out var port) ? port : null;
    }

    private static string? ParseNamed(string? text, string name)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var marker = name + "=";
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        return text[(index + marker.Length)..].Split(' ', 2)[0];
    }
}
