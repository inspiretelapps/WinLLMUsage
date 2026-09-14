using WinLLMUsage.Core;

namespace WinLLMUsage.App.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void UsesIndependentIdentity()
    {
        Assert.Equal("WinLLMUsage", AppInfo.ProductName);
        Assert.Equal("com.winllmusage.app", AppInfo.PackageId);
        Assert.NotEqual("com.robinebers.openusage", AppInfo.PackageId);
    }
}
