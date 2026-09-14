using WinLLMUsage.Core.Pricing;

namespace WinLLMUsage.Core.Tests;

public sealed class PricingStoreTests
{
    [Fact]
    public void LoadsBundledSupplement()
    {
        var store = new ModelPricingStore();
        Assert.NotEmpty(store.Current.Supplement.Models);
        var cost = store.Estimate("composer-1", 1_000_000, 0);
        Assert.NotNull(cost);
        Assert.True(cost > 0);
    }
}
