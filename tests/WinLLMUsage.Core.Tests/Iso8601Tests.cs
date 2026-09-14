using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.Tests;

public sealed class Iso8601Tests
{
    [Fact]
    public void FormatsMillisecondsAndZ()
    {
        var date = new DateTimeOffset(2026, 7, 13, 1, 40, 0, TimeSpan.Zero);
        Assert.Equal("2026-07-13T01:40:00.000Z", Iso8601.StringFrom(date));
    }

    [Fact]
    public void ParsesSpaceSeparatedUtc()
    {
        var parsed = Iso8601.DateFrom("2026-07-13 01:40:00 UTC");
        Assert.NotNull(parsed);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 1, 40, 0, TimeSpan.Zero), parsed);
    }

    [Fact]
    public void ParsesUnixSecondsAndMilliseconds()
    {
        var seconds = Iso8601.FromUnix(1_784_000_000);
        var millis = Iso8601.FromUnix(1_784_000_000_000);
        Assert.Equal(seconds, millis);
    }
}
