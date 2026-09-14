using System.Runtime.Versioning;

namespace WinLLMUsage.Windows;

public static class StartupRegistration
{
    public const string ValueName = "WinLLMUsage";

    public static bool IsEnabled(string? markerPath = null)
    {
        if (OperatingSystem.IsWindows())
        {
            return IsEnabledOnWindows();
        }

        return File.Exists(markerPath ?? DefaultMarker());
    }

    public static void SetEnabled(bool enabled, string launcherPath, string? markerPath = null)
    {
        if (OperatingSystem.IsWindows())
        {
            SetEnabledOnWindows(enabled, launcherPath);
            return;
        }

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

    [SupportedOSPlatform("windows")]
    private static bool IsEnabledOnWindows()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception)
        {
            return File.Exists(DefaultMarker());
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetEnabledOnWindows(bool enabled, string launcherPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{launcherPath}\"");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
            }
        }
        catch (Exception)
        {
        }
    }

    private static string DefaultMarker()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "WinLLMUsage", "launch-at-login");
    }
}
