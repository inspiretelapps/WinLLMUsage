using System.Globalization;
using System.Text.Json;

namespace WinLLMUsage.Providers.Shared;

public static class JsonObjectExtensions
{
    public static string? GetString(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    public static double? GetDouble(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return null;
    }

    public static long? GetInt64(this JsonElement element, params string[] names)
    {
        var value = element.GetDouble(names);
        return value is null ? null : (long)Math.Round(value.Value);
    }

    public static bool? GetBoolean(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }
        }

        return null;
    }

    public static JsonElement? GetObject(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return value;
            }
        }

        return null;
    }
}
