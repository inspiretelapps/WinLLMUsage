using WinLLMUsage.Windows;

namespace WinLLMUsage.Windows.Tests;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void RoundTripsMarkerFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "winllmusage-startup-" + Guid.NewGuid().ToString("n"));
        try
        {
            StartupRegistration.SetEnabled(true, "C:\\Program Files\\WinLLMUsage\\WinLLMUsage.exe", path);
            Assert.True(StartupRegistration.IsEnabled(path));
            StartupRegistration.SetEnabled(false, "unused", path);
            Assert.False(StartupRegistration.IsEnabled(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
