using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Layout;

namespace WinLLMUsage.Core.Tests;

public sealed class DefaultLayoutTests
{
    [Fact]
    public void PinsCapIsTwo()
    {
        Assert.Equal(2, DefaultLayout.MaxPinsPerProvider);
        var pinsByFamily = DefaultLayout.PinnedMetricIds
            .GroupBy(id => id.Split('.')[0]);
        foreach (var group in pinsByFamily)
        {
            Assert.True(group.Count() <= 2, group.Key);
        }
    }

    [Fact]
    public void SchemaV3RemapsDeadIds()
    {
        Assert.Equal("antigravity.geminiPro", DefaultLayout.SchemaV3Remaps["antigravity.session"]);
        Assert.Equal("copilot.premium", DefaultLayout.SchemaV3Remaps["copilot.credits"]);
    }

    [Fact]
    public void AllDefaultMetricsExistInCatalogExceptOptionalOffRows()
    {
        var catalog = KnownProviders.AllDescriptors();
        foreach (var id in DefaultLayout.MetricIds)
        {
            var family = id.Split('.')[0];
            Assert.Contains(family, catalog.Keys);
            Assert.Contains(catalog[family], d => d.Id == id || d.Id.EndsWith(id[family.Length..], StringComparison.Ordinal));
        }
    }
}
