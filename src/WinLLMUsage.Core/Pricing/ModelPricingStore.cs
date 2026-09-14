using System.Text.Json;

namespace WinLLMUsage.Core.Pricing;

public sealed class ModelPricingStore : Contracts.IPricingService
{
    private readonly ModelPricing _pricing;

    public ModelPricingStore(string? fallbackModel = null)
    {
        _pricing = new ModelPricing
        {
            Supplement = LoadSupplement(),
            Primary = LoadCatalog("pricing_litellm_snapshot.json"),
            Secondary = LoadCatalog("pricing_models_dev_snapshot.json"),
            FallbackModel = fallbackModel,
        };
    }

    public ModelPricing Current => _pricing;

    public double? Estimate(
        string model,
        long inputTokens,
        long outputTokens,
        long cacheReadTokens = 0,
        long cacheWriteTokens = 0,
        double priorityMultiplier = 1) =>
        _pricing.EstimateCost(model, inputTokens, outputTokens, cacheReadTokens, cacheWriteTokens, priorityMultiplier);

    private static PricingCatalog LoadSupplement()
    {
        var catalog = new PricingCatalog();
        using var doc = BundledPricing.Load("pricing_supplement.json");
        if (doc is null)
        {
            return catalog;
        }

        var root = doc.RootElement;
        if (root.TryGetProperty("updated_at", out var updated) && DateTimeOffset.TryParse(updated.GetString(), out var at))
        {
            catalog.UpdatedAt = at;
        }

        if (root.TryGetProperty("pricing", out var pricing))
        {
            foreach (var property in pricing.EnumerateObject())
            {
                var rates = ReadRates(property.Value);
                if (rates is not null)
                {
                    catalog.Models[property.Name] = rates;
                }
            }
        }

        if (root.TryGetProperty("aliases", out var aliases))
        {
            foreach (var property in aliases.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    catalog.Aliases[property.Name] = property.Value.GetString() ?? property.Name;
                }
            }
        }

        return catalog;
    }

    private static PricingCatalog LoadCatalog(string resource)
    {
        var catalog = new PricingCatalog();
        using var doc = BundledPricing.Load(resource);
        if (doc is null)
        {
            return catalog;
        }

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith('$'))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var rates = ReadRates(property.Value);
                if (rates is not null)
                {
                    catalog.Models[property.Name] = rates;
                }
            }
        }

        return catalog;
    }

    private static ModelRates? ReadRates(JsonElement element)
    {
        var input = Number(element, "input_per_million", "input_cost_per_token", "input") ?? 0;
        var output = Number(element, "output_per_million", "output_cost_per_token", "output") ?? 0;
        if (element.TryGetProperty("input_cost_per_token", out _) && input < 1)
        {
            input *= 1_000_000;
        }

        if (element.TryGetProperty("output_cost_per_token", out _) && output < 1)
        {
            output *= 1_000_000;
        }

        if (input <= 0 && output <= 0)
        {
            return null;
        }

        return new ModelRates(
            input,
            output,
            Number(element, "cache_read_per_million", "cache_read_input_token_cost") ?? 0,
            Number(element, "cache_write_per_million", "cache_creation_input_token_cost") ?? 0,
            Number(element, "cache_write_5m_per_million") ?? 0,
            Number(element, "cache_write_1h_per_million") ?? 0,
            element.TryGetProperty("max_tokens", out var ctx) && ctx.TryGetInt32(out var window) ? window : null);
    }

    private static double? Number(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n))
            {
                return n;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out n))
            {
                return n;
            }
        }

        return null;
    }
}
