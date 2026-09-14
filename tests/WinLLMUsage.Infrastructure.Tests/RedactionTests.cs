using WinLLMUsage.Infrastructure.Logging;

namespace WinLLMUsage.Infrastructure.Tests;

public sealed class RedactionTests
{
    [Fact]
    public void RedactsBearerAndJsonTokens()
    {
        var redacted = SecretRedactor.Redact("""Bearer abcdef {"access_token":"secret"} api_key=supersecret""");
        Assert.DoesNotContain("abcdef", redacted);
        Assert.DoesNotContain("supersecret", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }
}
