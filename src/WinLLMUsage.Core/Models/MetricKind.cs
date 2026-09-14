using System.Text.Json.Serialization;

namespace WinLLMUsage.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MetricKind>))]
public enum MetricKind
{
    [JsonStringEnumMemberName("percent")]
    Percent,

    [JsonStringEnumMemberName("dollars")]
    Dollars,

    [JsonStringEnumMemberName("count")]
    Count,
}
