using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public interface IDnsAgentClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task ApplyProfileAsync(
        DnsProfile profile,
        string? adapterSelection = null,
        CancellationToken cancellationToken = default);

    Task ResetToDhcpAsync(
        string? adapterSelection = null,
        CancellationToken cancellationToken = default);
}
