namespace WinLLMUsage.Core.Cli;

public sealed record CliArguments(string? ProviderId = null, bool Force = false, bool ShowHelp = false, bool ShowVersion = false);

public abstract class CliException : Exception
{
    protected CliException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public sealed class CliUsageException : CliException
{
    public CliUsageException(string message)
        : base(message, 2)
    {
    }
}

public sealed class CliReadException : CliException
{
    public CliReadException(string message)
        : base(message, 4)
    {
    }
}

public static class CliArgumentParser
{
    public static CliArguments Parse(IReadOnlyList<string> arguments)
    {
        string? providerId = null;
        var force = false;
        var help = false;
        var version = false;

        foreach (var argument in arguments)
        {
            switch (argument)
            {
                case "--force":
                    force = true;
                    break;
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "-v":
                case "--version":
                    version = true;
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new CliUsageException($"Unknown option: {argument}");
                    }

                    if (providerId is not null)
                    {
                        throw new CliUsageException("Only one provider can be requested at a time.");
                    }

                    providerId = argument.ToLowerInvariant();
                    break;
            }
        }

        return new CliArguments(providerId, force, help, version);
    }

    public const string Help = """
        Usage: winllmusage [provider] [--force]

        Read limits through WinLLMUsage's shared five-minute cache and exit. Output is always JSON.

        Options:
          --force      Refresh even when the shared cache is still fresh
          -v, --version
          -h, --help
        """;
}
