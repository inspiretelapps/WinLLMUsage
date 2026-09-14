using System.Text.Json;
using System.Text.Json.Serialization;
using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Serialization;

public static class JsonDefaults
{
    public static JsonSerializerOptions Cache { get; } = Create(writeIndented: false, sortedKeys: false);

    public static JsonSerializerOptions Limits { get; } = Create(writeIndented: false, sortedKeys: true);

    public static JsonSerializerOptions Usage { get; } = Create(writeIndented: false, sortedKeys: false);

    private static JsonSerializerOptions Create(bool writeIndented, bool sortedKeys)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new ProgressFormatJsonConverter());
        options.Converters.Add(new MetricLineJsonConverter());
        if (sortedKeys)
        {
            options.TypeInfoResolver = new SortedKeyResolver();
        }

        return options;
    }
}

/// <summary>
/// System.Text.Json does not sort object keys by default. Limits output matches Swift's
/// JSONEncoder.outputFormatting = [.sortedKeys] by encoding through a sorted dictionary wrapper
/// in <see cref="LocalLimitsApi"/> rather than relying on this resolver for every type.
/// </summary>
file sealed class SortedKeyResolver : System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver
{
    public System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
}
