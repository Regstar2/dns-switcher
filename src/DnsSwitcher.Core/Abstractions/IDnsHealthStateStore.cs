using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsHealthStateStore
{
    Task<DnsHealthState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DnsHealthState state, CancellationToken cancellationToken = default);
}
