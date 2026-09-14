namespace WinLLMUsage.Core.Models;

public sealed record MetricChartPoint(double Value, string Label, string? ValueLabel = null)
{
    public string Readout => ValueLabel ?? Formatting.MetricFormatter.Number(Value, MetricKind.Count, Formatting.FormatStyle.Row) + " tokens";
}
