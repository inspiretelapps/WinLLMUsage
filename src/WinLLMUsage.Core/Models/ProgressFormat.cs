using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinLLMUsage.Core.Models;

[JsonConverter(typeof(ProgressFormatJsonConverter))]
public abstract record ProgressFormat
{
    public static readonly ProgressFormat Percent = new PercentFormat();
    public static readonly ProgressFormat Dollars = new DollarsFormat();
    public static ProgressFormat Count(string suffix) => new CountFormat(suffix);

    public abstract MetricKind MetricKind { get; }
    public virtual string? CountSuffix => null;

    public sealed record PercentFormat : ProgressFormat
    {
        public override MetricKind MetricKind => MetricKind.Percent;
    }

    public sealed record DollarsFormat : ProgressFormat
    {
        public override MetricKind MetricKind => MetricKind.Dollars;
    }

    public sealed record CountFormat(string Suffix) : ProgressFormat
    {
        public override MetricKind MetricKind => MetricKind.Count;
        public override string? CountSuffix => Suffix;
    }
}

public sealed class ProgressFormatJsonConverter : JsonConverter<ProgressFormat>
{
    public override ProgressFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var kind = root.GetProperty("kind").GetString();
        return kind switch
        {
            "percent" => ProgressFormat.Percent,
            "dollars" => ProgressFormat.Dollars,
            "count" => ProgressFormat.Count(root.TryGetProperty("suffix", out var suffix) ? suffix.GetString() ?? "" : ""),
            _ => throw new JsonException($"Unknown progress format kind '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, ProgressFormat value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case ProgressFormat.PercentFormat:
                writer.WriteString("kind", "percent");
                break;
            case ProgressFormat.DollarsFormat:
                writer.WriteString("kind", "dollars");
                break;
            case ProgressFormat.CountFormat count:
                writer.WriteString("kind", "count");
                writer.WriteString("suffix", count.Suffix);
                break;
        }

        writer.WriteEndObject();
    }
}
