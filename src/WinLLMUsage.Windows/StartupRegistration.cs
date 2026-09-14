namespace WinLLMUsage.Windows;

/// <summary>
/// Per-user launch-at-login. On Windows the WPF host writes HKCU\...\Run; this helper
/// is the cross-platform fallback used by tests and the CLI.
/// </summary>
public static class StartupRegistration
{
    public const string ValueName = "WinLLMUsage";

    public static bool IsEnabled(string? markerPath = null) =>
        File.Exists(markerPath ?? DefaultMarker());

    public static void SetEnabled(bool enabled, string launcherPath, string? markerPath = null)
    {
        var path = markerPath ?? DefaultMarker();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (enabled)
        {
            File.WriteAllText(path, launcherPath);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string DefaultMarker()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "WinLLMUsage", "launch-at-login");
    }
}
