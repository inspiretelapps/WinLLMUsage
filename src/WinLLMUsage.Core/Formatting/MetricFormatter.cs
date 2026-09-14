using System.Globalization;
using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Formatting;

public enum FormatStyle
{
    Tray,
    Row,
    Full,
}

public static class MetricFormatter
{
    private static readonly CultureInfo Locale = CultureInfo.GetCultureInfo("en-US");

    public static string Number(double value, MetricKind kind, FormatStyle style)
    {
        return kind switch
        {
            MetricKind.Percent => $"{(int)Math.Round(ClampPercent(value))}%",
            MetricKind.Dollars => FormatDollars(value, style),
            MetricKind.Count => FormatCount(value, style),
            _ => value.ToString(Locale),
        };
    }

    public static string StringFor(MetricValue value, FormatStyle style)
    {
        var text = Number(value.Number, value.Kind, style);
        return string.IsNullOrEmpty(value.Label) ? text : $"{text} {value.Label}";
    }

    public static string LegacyCombined(IReadOnlyList<MetricValue> values) =>
        string.Join(" · ", values.Select(v => StringFor(v, v.Kind == MetricKind.Count ? FormatStyle.Tray : FormatStyle.Full)));

    public static double ClampPercent(double value) => Math.Clamp(value, 0, 100);

    private static string FormatDollars(double value, FormatStyle style)
    {
        if (Math.Abs(value) >= 1000 && style != FormatStyle.Full)
        {
            return "$" + Compact(value);
        }

        return style switch
        {
            FormatStyle.Tray => "$" + Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", Locale),
            _ => "$" + value.ToString("N2", Locale),
        };
    }

    private static string FormatCount(double value, FormatStyle style)
    {
        if (style != FormatStyle.Full && Math.Abs(value) >= 1000)
        {
            return Compact(value);
        }

        if (Math.Abs(value - Math.Round(value)) < 0.05)
        {
            return Math.Round(value).ToString("N0", Locale);
        }

        return value.ToString("0.#", Locale);
    }

    private static string Compact(double value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : "";
        if (abs >= 1_000_000_000)
        {
            return sign + Trim((abs / 1_000_000_000).ToString("0.#", Locale)) + "B";
        }

        if (abs >= 1_000_000)
        {
            return sign + Trim((abs / 1_000_000).ToString("0.#", Locale)) + "M";
        }

        return sign + Trim((abs / 1_000).ToString("0.#", Locale)) + "K";
    }

    private static string Trim(string value) => value.TrimEnd('0').TrimEnd('.');
}
