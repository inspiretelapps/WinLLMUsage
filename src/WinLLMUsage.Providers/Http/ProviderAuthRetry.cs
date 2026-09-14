using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Providers.Http;

public static class ProviderAuthRetry
{
    public static async Task<HttpResponse> FetchAsync(
        IHttpTransport transport,
        Func<CancellationToken, Task<HttpRequest>> build,
        Func<CancellationToken, Task<bool>> refresh,
        CancellationToken cancellationToken)
    {
        var first = await transport.SendAsync(await build(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        if (first.Status is not 401 and not 403)
        {
            return first;
        }

        if (!await refresh(cancellationToken).ConfigureAwait(false))
        {
            return first;
        }

        var retry = await transport.SendAsync(await build(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return retry;
    }

    public static ErrorCategory Classify(int status) => ErrorCategoryWire.FromHttpStatus(status);
}

public static class JsonRequest
{
    public static HttpRequest Get(string url, IReadOnlyDictionary<string, string> headers, TimeSpan? timeout = null) =>
        new(HttpMethod.Get, new Uri(url), headers, Timeout: timeout);

    public static HttpRequest PostJson(string url, string json, IReadOnlyDictionary<string, string> headers, TimeSpan? timeout = null) =>
        new(HttpMethod.Post, new Uri(url), headers, System.Text.Encoding.UTF8.GetBytes(json), timeout);

    public static HttpRequest PostForm(string url, string form, IReadOnlyDictionary<string, string> headers, TimeSpan? timeout = null)
    {
        var merged = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/x-www-form-urlencoded",
        };
        return new HttpRequest(HttpMethod.Post, new Uri(url), merged, System.Text.Encoding.UTF8.GetBytes(form), timeout);
    }
}
