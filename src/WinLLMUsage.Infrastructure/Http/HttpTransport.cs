using System.Net;
using System.Net.Http.Headers;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Settings;

namespace WinLLMUsage.Infrastructure.Http;

public sealed class HttpTransport : IHttpTransport, IDisposable
{
    private readonly HttpMessageHandler _handler;
    private readonly HttpClient _client;
    private readonly bool _ownsHandler;

    public HttpTransport(ProxySettings? proxy = null, HttpMessageHandler? handler = null)
    {
        if (handler is not null)
        {
            _handler = handler;
            _ownsHandler = false;
        }
        else
        {
            var sockets = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            if (proxy is { Enabled: true, Url: { Length: > 0 } url } && Uri.TryCreate(url, UriKind.Absolute, out var proxyUri))
            {
                sockets.Proxy = new WebProxy(proxyUri)
                {
                    Credentials = string.IsNullOrEmpty(proxyUri.UserInfo) ? null : ParseCredentials(proxyUri.UserInfo),
                    BypassProxyOnLocal = true,
                };
                sockets.UseProxy = true;
            }

            _handler = sockets;
            _ownsHandler = true;
        }

        _client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(request.Method, request.Url);
        if (request.Headers is not null)
        {
            foreach (var (key, value) in request.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(key, value))
                {
                    message.Content ??= new ByteArrayContent(request.Body ?? []);
                    message.Content.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        if (request.Body is { Length: > 0 } && message.Content is null)
        {
            message.Content = new ByteArrayContent(request.Body);
            message.Content.Headers.ContentType ??= new MediaTypeHeaderValue("application/json");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(15));
        using var response = await _client.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join(",", g.SelectMany(v => v.Value)), StringComparer.OrdinalIgnoreCase);
        return new HttpResponse((int)response.StatusCode, headers, body);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (_ownsHandler)
        {
            _handler.Dispose();
        }
    }

    private static NetworkCredential ParseCredentials(string userInfo)
    {
        var parts = userInfo.Split(':', 2);
        return new NetworkCredential(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
    }
}

public sealed class RecordingHttpTransport : IHttpTransport
{
    private readonly Func<HttpRequest, HttpResponse> _handler;

    public RecordingHttpTransport(Func<HttpRequest, HttpResponse> handler) => _handler = handler;

    public List<HttpRequest> Requests { get; } = [];

    public Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_handler(request));
    }
}
