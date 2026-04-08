using DnsSwitcher.Core.Abstractions;
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
}
