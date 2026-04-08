using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public static class NetworkAdapterSelector
{
    public static NetworkAdapter? SelectDefault(IEnumerable<NetworkAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var candidates = adapters
            .Where(adapter => !adapter.IsLoopback && adapter.SupportedStacks != NetworkStackSupport.None)
            .ToList();

        return Select(candidates, adapter => adapter.IsActive && adapter.IsPhysical && adapter.HasDefaultGateway)
            ?? Select(candidates, adapter => adapter.IsActive && adapter.HasDefaultGateway)
            ?? Select(candidates, adapter => adapter.IsActive && adapter.IsPhysical)
            ?? Select(candidates, adapter => adapter.IsActive)
            ?? Select(candidates, adapter => adapter.IsPhysical)
            ?? OrderCandidates(candidates).FirstOrDefault();
    }

    private static NetworkAdapter? Select(
        IReadOnlyList<NetworkAdapter> adapters,
        Func<NetworkAdapter, bool> predicate)
    {
        return OrderCandidates(adapters.Where(predicate)).FirstOrDefault();
    }

    private static IOrderedEnumerable<NetworkAdapter> OrderCandidates(IEnumerable<NetworkAdapter> adapters)
    {
        return adapters
            .OrderByDescending(adapter => GetStackScore(adapter.SupportedStacks))
            .ThenByDescending(adapter => adapter.HasDefaultGateway)
            .ThenBy(adapter => adapter.InterfaceIndex ?? int.MaxValue)
            .ThenBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetStackScore(NetworkStackSupport supportedStacks)
    {
        return supportedStacks switch
        {
            NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6 => 2,
            NetworkStackSupport.Ipv4 or NetworkStackSupport.Ipv6 => 1,
            _ => 0,
        };
    }
}
