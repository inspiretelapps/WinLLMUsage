using System.Globalization;
using System.Text.RegularExpressions;

namespace WinLLMUsage.Core.Time;

/// <summary>
/// Shared ISO-8601 formatting/parsing matching OpenUsage's OpenUsageISO8601.
/// Output always uses millisecond precision and a Z suffix.
/// </summary>
public static partial class Iso8601
{
    public static string StringFrom(DateTimeOffset date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset? DateFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = NormalizeTimestamp(value);
        if (DateTimeOffset.TryParseExact(
                normalized,
                ["yyyy-MM-ddTHH:mm:ss.fffK", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return parsed;
        }

        return null;
    }

    public static DateTimeOffset? FromUnix(double epoch)
    {
        // Swift: seconds if |n| < 1e10 else milliseconds.
        var seconds = Math.Abs(epoch) < 10_000_000_000d ? epoch : epoch / 1000d;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000d));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string NormalizeTimestamp(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0)
        {
            return s;
        }

        var spaceMatch = SpaceTimestampRegex().Match(s);
        if (spaceMatch.Success)
        {
            s = s[..spaceMatch.Index] + spaceMatch.Value.Replace(' ', 'T') + s[(spaceMatch.Index + spaceMatch.Length)..];
        }

        if (s.EndsWith(" UTC", StringComparison.Ordinal))
        {
            s = s[..^4] + "Z";
        }

        var full = FullIsoRegex().Match(s);
        if (full.Success)
        {
            return NormalizeFractionalIso(full.Value, assumeUtc: false);
        }

        var head = HeadIsoRegex().Match(s);
        if (head.Success)
        {
            return NormalizeFractionalIso(head.Value, assumeUtc: true);
        }

        return s;
    }

    private static string NormalizeFractionalIso(string value, bool assumeUtc)
    {
        var match = FractionalIsoRegex().Match(value);
        if (!match.Success)
        {
            return assumeUtc && !value.EndsWith('Z') ? value + "Z" : value;
        }

        var head = match.Groups[1].Value;
        var frac = "";
        if (match.Groups[2].Success)
        {
            var digits = match.Groups[2].Value[1..];
            if (digits.Length > 3)
            {
                digits = digits[..3];
            }

            while (digits.Length < 3)
            {
                digits += "0";
            }

            frac = "." + digits;
        }

        var tz = "Z";
        if (!assumeUtc && match.Groups[3].Success)
        {
            tz = match.Groups[3].Value;
        }

        return head + frac + tz;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}")]
    private static partial Regex SpaceTimestampRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex FullIsoRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$")]
    private static partial Regex HeadIsoRegex();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?(Z|[+-]\d{2}:\d{2})?$")]
    private static partial Regex FractionalIsoRegex();
}
