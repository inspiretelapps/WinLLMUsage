using WinLLMUsage.Infrastructure.Paths;

namespace WinLLMUsage.Windows;

public static class KnownFolders
{
    public static AppPaths Create(string? overrideRoot = null) => new(overrideRoot);
}
