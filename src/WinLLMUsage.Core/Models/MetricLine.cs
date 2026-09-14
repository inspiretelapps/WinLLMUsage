using System.Text.Json;
using System.Text.Json.Serialization;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.Models;

[JsonConverter(typeof(MetricLineJsonConverter))]
public abstract record MetricLine
{
    public const string ErrorBadgeLabel = "Error";

    public static readonly MetricLine NoUsageData = Badge("Status", "No usage data", "#A3A3A3");

    public abstract string Label { get; }

    public bool IsError => this is BadgeLine line && line.Label == ErrorBadgeLabel;

    public static MetricLine Text(string label, string value, string? colorHex = null, string? subtitle = null) =>
        new TextLine(label, value, colorHex, subtitle);

    public static MetricLine Values(
        string label,
        IReadOnlyList<MetricValue> values,
        string? colorHex = null,
        IReadOnlyList<DateTimeOffset>? expiriesAt = null,
        IReadOnlyList<string>? unknownModels = null,
        ModelUsageBreakdown? modelBreakdown = null) =>
        new ValuesLine(label, values, colorHex, expiriesAt ?? [], unknownModels ?? [], modelBreakdown);

    public static MetricLine Progress(
        string label,
        double used,
        double limit,
        ProgressFormat format,
        DateTimeOffset? resetsAt = null,
        int? periodDurationMs = null,
        string? colorHex = null) =>
        new ProgressLine(label, used, limit, format, resetsAt, periodDurationMs, colorHex);

    public static MetricLine Badge(string label, string text, string? colorHex = null, string? subtitle = null) =>
        new BadgeLine(label, text, colorHex, subtitle);

    public static MetricLine Chart(string label, IReadOnlyList<MetricChartPoint> points, string? note = null) =>
        new ChartLine(label, points, note);

    public static void AppendNoDataIfNeeded(List<MetricLine> lines)
    {
        if (lines.Count == 0)
        {
            lines.Add(NoUsageData);
        }
    }

    public sealed record TextLine(string LineLabel, string Value, string? ColorHex, string? Subtitle) : MetricLine
    {
        public override string Label => LineLabel;
    }

    public sealed record ValuesLine(
        string LineLabel,
        IReadOnlyList<MetricValue> Items,
        string? ColorHex,
        IReadOnlyList<DateTimeOffset> ExpiriesAt,
        IReadOnlyList<string> UnknownModels,
        ModelUsageBreakdown? ModelBreakdown) : MetricLine
    {
        public override string Label => LineLabel;
    }

    public sealed record ProgressLine(
        string LineLabel,
        double Used,
        double Limit,
        ProgressFormat Format,
        DateTimeOffset? ResetsAt,
        int? PeriodDurationMs,
        string? ColorHex) : MetricLine
    {
        public override string Label => LineLabel;
    }

    public sealed record BadgeLine(string LineLabel, string BadgeText, string? ColorHex, string? Subtitle) : MetricLine
    {
        public override string Label => LineLabel;
    }

    public sealed record ChartLine(string LineLabel, IReadOnlyList<MetricChartPoint> Points, string? Note) : MetricLine
    {
        public override string Label => LineLabel;
    }
}

public sealed class MetricLineJsonConverter : JsonConverter<MetricLine>
{
    public override MetricLine Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetString();
        var label = root.GetProperty("label").GetString() ?? "";
        return type switch
        {
            "text" => MetricLine.Text(
                label,
                root.GetProperty("value").GetString() ?? "",
                OptionalString(root, "colorHex"),
                OptionalString(root, "subtitle")),
            "values" => MetricLine.Values(
                label,
                JsonSerializer.Deserialize<List<MetricValue>>(root.GetProperty("values").GetRawText(), options) ?? [],
                OptionalString(root, "colorHex"),
                OptionalDates(root, "expiriesAt"),
                OptionalStringList(root, "unknownModels"),
                root.TryGetProperty("modelBreakdown", out var breakdown)
                    ? JsonSerializer.Deserialize<ModelUsageBreakdown>(breakdown.GetRawText(), options)
                    : null),
            "progress" => MetricLine.Progress(
                label,
                root.GetProperty("used").GetDouble(),
                root.GetProperty("limit").GetDouble(),
                JsonSerializer.Deserialize<ProgressFormat>(root.GetProperty("format").GetRawText(), options) ?? ProgressFormat.Percent,
                OptionalDate(root, "resetsAt"),
                root.TryGetProperty("periodDurationMs", out var period) && period.ValueKind == JsonValueKind.Number ? period.GetInt32() : null,
                OptionalString(root, "colorHex")),
            "badge" => MetricLine.Badge(
                label,
                root.GetProperty("text").GetString() ?? "",
                OptionalString(root, "colorHex"),
                OptionalString(root, "subtitle")),
            "chart" => MetricLine.Chart(
                label,
                JsonSerializer.Deserialize<List<MetricChartPoint>>(root.GetProperty("points").GetRawText(), options) ?? [],
                OptionalString(root, "note")),
            _ => throw new JsonException($"Unknown metric line type '{type}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, MetricLine value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case MetricLine.TextLine text:
                writer.WriteString("type", "text");
                writer.WriteString("label", text.Label);
                writer.WriteString("value", text.Value);
                WriteOptional(writer, "colorHex", text.ColorHex);
                WriteOptional(writer, "subtitle", text.Subtitle);
                break;
            case MetricLine.ValuesLine values:
                writer.WriteString("type", "values");
                writer.WriteString("label", values.Label);
                writer.WritePropertyName("values");
                JsonSerializer.Serialize(writer, values.Items, options);
                WriteOptional(writer, "colorHex", values.ColorHex);
                if (values.ExpiriesAt.Count > 0)
                {
                    writer.WritePropertyName("expiriesAt");
                    JsonSerializer.Serialize(writer, values.ExpiriesAt.Select(Iso8601.StringFrom).ToArray(), options);
                }

                if (values.UnknownModels.Count > 0)
                {
                    writer.WritePropertyName("unknownModels");
                    JsonSerializer.Serialize(writer, values.UnknownModels, options);
                }

                if (values.ModelBreakdown is not null)
                {
                    writer.WritePropertyName("modelBreakdown");
                    JsonSerializer.Serialize(writer, values.ModelBreakdown, options);
                }

                break;
            case MetricLine.ProgressLine progress:
                writer.WriteString("type", "progress");
                writer.WriteString("label", progress.Label);
                writer.WriteNumber("used", progress.Used);
                writer.WriteNumber("limit", progress.Limit);
                writer.WritePropertyName("format");
                JsonSerializer.Serialize(writer, progress.Format, options);
                if (progress.ResetsAt is { } reset)
                {
                    writer.WriteString("resetsAt", Iso8601.StringFrom(reset));
                }

                if (progress.PeriodDurationMs is { } period)
                {
                    writer.WriteNumber("periodDurationMs", period);
                }

                WriteOptional(writer, "colorHex", progress.ColorHex);
                break;
            case MetricLine.BadgeLine badge:
                writer.WriteString("type", "badge");
                writer.WriteString("label", badge.Label);
                writer.WriteString("text", badge.BadgeText);
                WriteOptional(writer, "colorHex", badge.ColorHex);
                WriteOptional(writer, "subtitle", badge.Subtitle);
                break;
            case MetricLine.ChartLine chart:
                writer.WriteString("type", "chart");
                writer.WriteString("label", chart.Label);
                writer.WritePropertyName("points");
                JsonSerializer.Serialize(writer, chart.Points, options);
                WriteOptional(writer, "note", chart.Note);
                break;
        }

        writer.WriteEndObject();
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string> OptionalStringList(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
            : [];

    private static IReadOnlyList<DateTimeOffset> OptionalDates(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(e => Iso8601.DateFrom(e.GetString()))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToArray();
    }

    private static DateTimeOffset? OptionalDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? Iso8601.DateFrom(value.GetString())
            : null;

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
