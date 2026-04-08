using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class NetworkAdapterService(INetworkAdapterProvider networkAdapterProvider)
{
    public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default)
    {
        return networkAdapterProvider.GetAdaptersAsync(cancellationToken);
    }

    public async Task<NetworkAdapter?> GetDefaultAdapterAsync(CancellationToken cancellationToken = default)
    {
        var adapters = await networkAdapterProvider.GetAdaptersAsync(cancellationToken).ConfigureAwait(false);
        return NetworkAdapterSelector.SelectDefault(adapters);
    }

    public async Task<NetworkAdapter?> GetSelectedAdapterAsync(
        string? adapterIdOrName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adapterIdOrName))
        {
            return await GetDefaultAdapterAsync(cancellationToken).ConfigureAwait(false);
        }

        var adapters = await networkAdapterProvider.GetAdaptersAsync(cancellationToken).ConfigureAwait(false);
        var exactIdMatch = adapters.FirstOrDefault(adapter =>
            string.Equals(adapter.Id, adapterIdOrName, StringComparison.OrdinalIgnoreCase));

        if (exactIdMatch is not null)
        {
            return exactIdMatch;
        }

        var nameMatches = adapters
            .Where(adapter => string.Equals(adapter.Name, adapterIdOrName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return nameMatches.Length switch
        {
            0 => null,
            1 => nameMatches[0],
            _ => throw new NetworkAdapterNotFoundException(
                $"Multiple network adapters match '{adapterIdOrName}'. Use adapter id instead."),
        };
    }
}
