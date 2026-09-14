using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Infrastructure.Cache;

namespace WinLLMUsage.Infrastructure.Tests;

public sealed class SnapshotCacheTests
{
    [Fact]
    public void GuiCacheIsNotFreshAfterReload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "winllmusage-cache-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "snapshots.json");
        var clock = new FrozenClock(DateTimeOffset.UtcNow);
        var provider = new Provider("codex", "Codex", "codex");
        var snapshot = ProviderSnapshot.Make(provider, "Pro", [MetricLine.Progress("Session", 10, 100, ProgressFormat.Percent)], clock.Now);
        try
        {
            var first = new SnapshotCache(path, clock, allowsPersistedFreshness: false);
            first.Store(snapshot, "acct");
            Assert.True(first.IsFresh("codex"));

            var gui = new SnapshotCache(path, clock, allowsPersistedFreshness: false);
            Assert.False(gui.IsFresh("codex"));

            var cli = new SnapshotCache(path, clock, allowsPersistedFreshness: true);
            Assert.True(cli.IsFresh("codex"));
            Assert.True(gui.HasStaleAccountStamp("codex", "other"));
            Assert.False(gui.HasStaleAccountStamp("codex", "acct"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
