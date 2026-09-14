using System.Globalization;
using System.Text.Json;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Ollama;

public static class OllamaUsageMapper
{
    public static IReadOnlyList<MetricLine> MapUsage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Ollama usage response is missing limits.");
        }

        var lines = new List<MetricLine>();
        if (Percent(limits, "session", "Session") is { } session)
        {
            lines.Add(session);
        }

        if (Percent(limits, "weekly", "Weekly") is { } weekly)
        {
            lines.Add(weekly);
        }

        if (doc.RootElement.GetObject("activity") is { } activity)
        {
            var cost = activity.GetDouble("cost");
            if (cost is { } dollars && dollars >= 0)
            {
                lines.Add(MetricLine.Values("Last 4 Weeks", [new MetricValue(dollars, MetricKind.Dollars)]));
            }
        }

        return lines.Count == 0 ? [MetricLine.NoUsageData] : lines;
    }

    public static string? PlanName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raw = doc.RootElement.GetString("Plan", "plan");
            return string.IsNullOrWhiteSpace(raw) ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MetricLine? Percent(JsonElement limits, string key, string label)
    {
        var entry = limits.GetObject(key);
        var fraction = entry?.GetDouble("usage");
        if (fraction is null)
        {
            return null;
        }

        return MetricLine.Progress(label, MetricFormatter.ClampPercent(fraction.Value * 100), 100, ProgressFormat.Percent);
    }
}

public static class OllamaRequestSigner
{
    public static string Authorization(Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters key, string method, string requestUri)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes($"{method},{requestUri}");
        var signer = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(payload, 0, payload.Length);
        var signature = Convert.ToBase64String(signer.GenerateSignature());
        var publicKey = Convert.ToBase64String(key.GeneratePublicKey().GetEncoded());
        return $"{publicKey}:{signature}";
    }
}
