using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsManager
{
    Task<DnsStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task ApplyProfileAsync(DnsProfile profile, CancellationToken cancellationToken = default);

    Task ResetToDhcpAsync(CancellationToken cancellationToken = default);
}
