using System.Text.Json;

namespace WinLLMUsage.Core.Pricing;

public sealed record ModelRates(
    double InputPerMillion,
    double OutputPerMillion,
    double CacheReadPerMillion = 0,
    double CacheWritePerMillion = 0,
    double CacheWrite5mPerMillion = 0,
    double CacheWrite1hPerMillion = 0,
    int? ContextWindow = null,
    int LongContextThreshold = 0,
    double LongContextInputMultiplier = 1,
    double LongContextOutputMultiplier = 1);

public sealed class PricingCatalog
{
    public Dictionary<string, ModelRates> Models { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset? UpdatedAt { get; set; }

    public ModelRates? Find(string model)
    {
        if (Models.TryGetValue(model, out var rates))
        {
            return rates;
        }

        if (Aliases.TryGetValue(model, out var canonical) && Models.TryGetValue(canonical, out rates))
        {
            return rates;
        }

        return null;
    }
}

public sealed class ModelPricing
{
    public static ModelPricing Empty { get; } = new();

    public PricingCatalog Supplement { get; init; } = new();
    public PricingCatalog Primary { get; init; } = new();
    public PricingCatalog Secondary { get; init; } = new();
    public string? FallbackModel { get; init; }

    /// <summary>
    /// Catalog precedence: supplement, then LiteLLM (primary), then models.dev (secondary),
    /// then the optional Codex fallback model. A priority multiplier is applied once by the caller.
    /// </summary>
    public ModelRates? RatesFor(string model)
    {
        return Supplement.Find(model)
               ?? Primary.Find(model)
               ?? Secondary.Find(model)
               ?? (string.IsNullOrWhiteSpace(FallbackModel) ? null : Supplement.Find(FallbackModel) ?? Primary.Find(FallbackModel) ?? Secondary.Find(FallbackModel));
    }

    public double? EstimateCost(
        string model,
        long inputTokens,
        long outputTokens,
        long cacheReadTokens = 0,
        long cacheWriteTokens = 0,
        double priorityMultiplier = 1)
    {
        var rates = RatesFor(model);
        if (rates is null)
        {
            return null;
        }

        var input = inputTokens / 1_000_000d * rates.InputPerMillion;
        var output = outputTokens / 1_000_000d * rates.OutputPerMillion;
        var cacheRead = cacheReadTokens / 1_000_000d * rates.CacheReadPerMillion;
        var cacheWrite = cacheWriteTokens / 1_000_000d * rates.CacheWritePerMillion;
        return (input + output + cacheRead + cacheWrite) * priorityMultiplier;
    }
}

public static class BundledPricing
{
    public static Stream? Open(string resourceName)
    {
        var assembly = typeof(BundledPricing).Assembly;
        var full = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
        return full is null ? null : assembly.GetManifestResourceStream(full);
    }

    public static JsonDocument? Load(string resourceName)
    {
        using var stream = Open(resourceName);
        return stream is null ? null : JsonDocument.Parse(stream);
    }
}
