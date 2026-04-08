using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsManager
{
    Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default);

    Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default);

    Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default);
}
