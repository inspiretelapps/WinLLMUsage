using WinLLMUsage.Core;
using WinLLMUsage.Core.Contracts;

namespace WinLLMUsage.Infrastructure.Paths;

public sealed class AppPaths : IProviderPaths
{
    public AppPaths(string? overrideRoot = null, string? userProfile = null, string? localAppData = null, string? roamingAppData = null)
    {
        UserProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LocalAppData = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        RoamingAppData = roamingAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataRoot = overrideRoot
                      ?? Environment.GetEnvironmentVariable("WINLLMUSAGE_DATA")
                      ?? Path.Combine(LocalAppData, AppInfo.DataDirectoryName);
    }

    public string UserProfile { get; }
    public string LocalAppData { get; }
    public string RoamingAppData { get; }
    public string AppDataRoot { get; }
    public string SettingsFile => Path.Combine(AppDataRoot, "settings.json");
    public string SnapshotsFile => Path.Combine(AppDataRoot, "snapshots", "snapshots.json");
    public string HistoryDirectory => Path.Combine(AppDataRoot, "history");
    public string ScanCacheDirectory => Path.Combine(AppDataRoot, "scan-cache");
    public string PricingDirectory => Path.Combine(AppDataRoot, "pricing");
    public string LogsDirectory => Path.Combine(AppDataRoot, "logs");
    public string SecretsDirectory => Path.Combine(AppDataRoot, "secrets");
    public string LogFile => Path.Combine(LogsDirectory, "WinLLMUsage.log");

    public IReadOnlyList<string> CandidateHomes(string providerFamily) => providerFamily switch
    {
        "claude" => Unique(
            Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
            Path.Combine(UserProfile, ".claude")),
        "codex" => Unique(
            Environment.GetEnvironmentVariable("CODEX_HOME"),
            Path.Combine(UserProfile, ".codex"),
            Path.Combine(UserProfile, ".config", "codex")),
        "grok" => Unique(
            Environment.GetEnvironmentVariable("GROK_HOME"),
            Path.Combine(UserProfile, ".grok")),
        "ollama" => Unique(Path.Combine(UserProfile, ".ollama")),
        "opencode" => Unique(
            Environment.GetEnvironmentVariable("OPENCODE_DATA_DIR"),
            CombineIfPresent(Environment.GetEnvironmentVariable("XDG_DATA_HOME"), "opencode"),
            Path.Combine(UserProfile, ".local", "share", "opencode"),
            Path.Combine(LocalAppData, "opencode")),
        "openrouter" => Unique(
            Path.Combine(UserProfile, ".config", "openusage", "openrouter.json"),
            Path.Combine(UserProfile, ".config", "winllmusage", "openrouter.json")),
        "zai" => Unique(
            Path.Combine(UserProfile, ".config", "openusage", "zai.json"),
            Path.Combine(UserProfile, ".config", "winllmusage", "zai.json")),
        "cursor" => Unique(
            Path.Combine(RoamingAppData, "Cursor", "User", "globalStorage", "state.vscdb")),
        "copilot" => Unique(
            Path.Combine(UserProfile, ".config", "github-copilot"),
            Environment.GetEnvironmentVariable("GH_CONFIG_DIR"),
            Path.Combine(UserProfile, ".config", "gh")),
        "devin" => Unique(
            Path.Combine(UserProfile, ".devin"),
            Path.Combine(RoamingAppData, "Devin", "User", "globalStorage", "state.vscdb")),
        "antigravity" => Unique(
            Path.Combine(UserProfile, ".gemini"),
            Path.Combine(UserProfile, ".antigravity")),
        _ => Unique(Path.Combine(UserProfile, "." + providerFamily)),
    };

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotsFile)!);
        Directory.CreateDirectory(HistoryDirectory);
        Directory.CreateDirectory(ScanCacheDirectory);
        Directory.CreateDirectory(PricingDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(SecretsDirectory);
    }

    private static IReadOnlyList<string> Unique(params string?[] paths) =>
        paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string? CombineIfPresent(string? root, string child) =>
        string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, child);
}
