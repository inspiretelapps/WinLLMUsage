using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Infrastructure.Api;

namespace WinLLMUsage.Infrastructure.Tests;

public sealed class LocalUsageServerTests
{
    [Fact]
    public async Task ServesLimitsOnLoopback()
    {
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = ["zai"],
            KnownIds = new HashSet<string> { "zai" },
            Snapshots = new Dictionary<string, ProviderSnapshot>
            {
                ["zai"] = new("zai", "Z.ai", [MetricLine.Progress("Session", 1, 100, ProgressFormat.Percent)], DateTimeOffset.UtcNow),
            },
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        await using var server = new LocalUsageServer(() => state, NullLogger.Instance);
        await server.StartAsync(CancellationToken.None);
        if (!server.IsRunning)
        {
            return;
        }

        using var client = new TcpClient();
        await client.ConnectAsync(System.Net.IPAddress.Loopback, 6736);
        var stream = client.GetStream();
        var request = Encoding.ASCII.GetBytes("GET /v1/limits HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
        await stream.WriteAsync(request);
        using var reader = new StreamReader(stream);
        var response = await reader.ReadToEndAsync();
        Assert.Contains("HTTP/1.1 200", response);
        Assert.Contains("openusage.limits.v1", response);
        Assert.Contains("Access-Control-Allow-Origin: *", response);
    }
}
