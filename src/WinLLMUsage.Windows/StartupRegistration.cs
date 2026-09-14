namespace WinLLMUsage.Windows;

public static class StartupRegistration
{
    public const string ValueName = "WinLLMUsage";

    public static bool IsEnabled(string? markerPath = null)
    {
#if WINDOWS
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (key?.GetValue(ValueName) is string)
            {
                return true;
            }
        }
        catch (Exception)
        {
        }
#endif
        return File.Exists(markerPath ?? DefaultMarker());
    }

    public static void SetEnabled(bool enabled, string launcherPath, string? markerPath = null)
    {
#if WINDOWS
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is not null)
            {
                if (enabled)
                {
                    key.SetValue(ValueName, $"\"{launcherPath}\"");
                }
                else if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName);
                }

                return;
            }
        }
        catch (Exception)
        {
        }
#endif
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
