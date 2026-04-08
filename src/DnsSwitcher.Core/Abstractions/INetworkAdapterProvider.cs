using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface INetworkAdapterProvider
{
    Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default);
}
