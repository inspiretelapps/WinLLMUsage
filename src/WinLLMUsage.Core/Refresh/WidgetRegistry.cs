using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Layout;
using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Refresh;

public sealed class WidgetRegistry
{
    public WidgetRegistry(IReadOnlyList<IProviderRuntime> providers)
    {
        Providers = providers;
        DescriptorsByProvider = providers.ToDictionary(
            p => p.Provider.Id,
            p => p.WidgetDescriptors,
            StringComparer.Ordinal);
        LimitDescriptorsByProvider = providers.ToDictionary(
            p => p.Provider.Id,
            IReadOnlyList<WidgetDescriptor> (p) => p.WidgetDescriptors.Where(d => d.LimitResources.Count > 0).ToArray(),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<IProviderRuntime> Providers { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<WidgetDescriptor>> DescriptorsByProvider { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<WidgetDescriptor>> LimitDescriptorsByProvider { get; }

    public IReadOnlyList<string> OrderedProviderIds(IReadOnlyList<string>? savedOrder)
    {
        var known = Providers.Select(p => p.Provider.Id).ToList();
        if (savedOrder is null || savedOrder.Count == 0)
        {
            return OrderByCanonical(known);
        }

        var ordered = savedOrder.Where(known.Contains).ToList();
        foreach (var id in OrderByCanonical(known))
        {
            if (!ordered.Contains(id))
            {
                ordered.Add(id);
            }
        }

        return ordered;
    }

    private static IReadOnlyList<string> OrderByCanonical(IReadOnlyList<string> ids)
    {
        var rank = DefaultLayout.CanonicalProviderOrder
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index, StringComparer.Ordinal);
        return ids
            .OrderBy(id => rank.GetValueOrDefault(Accounts.ProviderAccountId.FamilyOf(id), 1000))
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
