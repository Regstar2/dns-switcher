using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsProfileActivator
{
    Task ApplyTransientProfileAsync(
        DnsProfile profile,
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default);

    Task ResetToDhcpTransientAsync(
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default);
}
