namespace WinLLMUsage.Core.Models;

public sealed record MetricValue(double Number, MetricKind Kind, string? Label = null, bool Estimated = false);

public abstract record ValueSelection
{
    public static readonly ValueSelection All = new AllSelection();

    public static ValueSelection Kind(MetricKind kind) => new KindSelection(kind);

    public abstract IReadOnlyList<MetricValue> Apply(IReadOnlyList<MetricValue> values);

    private sealed record AllSelection : ValueSelection
    {
        public override IReadOnlyList<MetricValue> Apply(IReadOnlyList<MetricValue> values) => values;
    }

    private sealed record KindSelection(MetricKind KindValue) : ValueSelection
    {
        public override IReadOnlyList<MetricValue> Apply(IReadOnlyList<MetricValue> values) =>
            values.Where(v => v.Kind == KindValue).ToArray();
    }
}
