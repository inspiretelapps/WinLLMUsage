using WinLLMUsage.Core.Cli;

namespace WinLLMUsage.Core.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void ParsesProviderAndForce()
    {
        var parsed = CliArgumentParser.Parse(["codex", "--force"]);
        Assert.Equal("codex", parsed.ProviderId);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void LowercasesProvider()
    {
        Assert.Equal("claude", CliArgumentParser.Parse(["Claude"]).ProviderId);
    }

    [Fact]
    public void RejectsUnknownOption()
    {
        var error = Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["--nope"]));
        Assert.Equal(2, error.ExitCode);
    }

    [Fact]
    public void RejectsTwoProviders()
    {
        Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["claude", "codex"]));
    }
}
