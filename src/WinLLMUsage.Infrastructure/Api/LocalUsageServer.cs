using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using WinLLMUsage.Core;
using WinLLMUsage.Core.Serialization;

namespace WinLLMUsage.Infrastructure.Api;

public sealed class LocalUsageServer : IAsyncDisposable
{
    private readonly Func<LocalUsageApi.State> _state;
    private readonly ILogger _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _active;

    public LocalUsageServer(Func<LocalUsageApi.State> state, ILogger logger)
    {
        _state = state;
        _logger = logger;
    }

    public bool IsRunning => _listener is not null;

    public string? UnavailableReason { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, AppInfo.LocalApiPort);
            _listener.Start();
        }
        catch (SocketException ex)
        {
            UnavailableReason = $"Port {AppInfo.LocalApiPort} is in use ({ex.SocketErrorCode}). The local API is unavailable.";
            _logger.LogWarning("{Message}", UnavailableReason);
            _listener = null;
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoop = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            if (Interlocked.Increment(ref _active) > AppInfo.LocalApiMaxConnections)
            {
                Interlocked.Decrement(ref _active);
                await WriteResponseAsync(stream, LocalUsageApi.Busy, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)))
                {
                }

                var parts = requestLine.Split(' ');
                var method = parts.Length > 0 ? parts[0] : "GET";
                var path = parts.Length > 1 ? parts[1] : "/";
                var response = LocalUsageApi.Respond(method, path, _state());
                await WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, LocalUsageApi.Response response, CancellationToken cancellationToken)
    {
        var body = response.Body ?? [];
        var statusText = response.Status switch
        {
            200 => "OK",
            204 => "No Content",
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            _ => "Error",
        };
        var header = new StringBuilder()
            .Append("HTTP/1.1 ").Append(response.Status).Append(' ').Append(statusText).Append("\r\n")
            .Append("Access-Control-Allow-Origin: *\r\n")
            .Append("Access-Control-Allow-Methods: GET, OPTIONS\r\n")
            .Append("Access-Control-Allow-Headers: Content-Type\r\n")
            .Append("Connection: close\r\n");
        if (response.Status != 204)
        {
            header.Append("Content-Type: application/json\r\n");
            header.Append("Content-Length: ").Append(body.Length).Append("\r\n");
        }

        header.Append("\r\n");
        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
    }
}
