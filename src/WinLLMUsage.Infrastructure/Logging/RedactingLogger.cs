using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WinLLMUsage.Infrastructure.Logging;

public static partial class SecretRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = BearerRegex().Replace(value, "$1[REDACTED]");
        redacted = KeyRegex().Replace(redacted, "$1[REDACTED]");
        redacted = TokenJsonRegex().Replace(redacted, "$1\"[REDACTED]\"");
        redacted = AuthorizationHeaderRegex().Replace(redacted, "$1[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"(Bearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"((?:api[_-]?key|access[_-]?token|refresh[_-]?token|secret|password)\s*[:=]\s*)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex KeyRegex();

    [GeneratedRegex(@"(""(?:access_token|refresh_token|apiKey|api_key|id_token|password)""\s*:\s*)""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex TokenJsonRegex();

    [GeneratedRegex(@"(Authorization:\s*)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationHeaderRegex();
}

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _gate = new();
    private const long MaxBytes = 10 * 1024 * 1024;

    public FileLoggerProvider(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Write(string category, LogLevel level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} {level} {category} {SecretRedactor.Redact(message)}{Environment.NewLine}";
        lock (_gate)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, line);
        }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        var rotated = Path.Combine(Path.GetDirectoryName(_path)!, Path.GetFileNameWithoutExtension(_path) + ".1.log");
        if (File.Exists(rotated))
        {
            File.Delete(rotated);
        }

        File.Move(_path, rotated);
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null)
            {
                message += " " + SecretRedactor.Redact(exception.ToString());
            }

            provider.Write(category, logLevel, message);
        }
    }
}
