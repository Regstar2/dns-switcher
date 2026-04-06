using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

public sealed class WindowsDnsManager(ILogger<WindowsDnsManager> logger) : IDnsManager
{
    public Task<DnsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new DnsStatus(
            IsManaged: false,
            ActiveProfileId: null,
            AdapterName: null,
            NameServers: [],
            Details: "System DNS inspection is not implemented in v0.1.");

        return Task.FromResult(status);
    }

    public Task ApplyProfileAsync(DnsProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        logger.LogWarning("DNS profile apply is not implemented in v0.1. Profile: {ProfileId}", profile.Id);
        throw new NotSupportedException("DNS profile apply is not implemented in v0.1.");
    }

    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("DNS disable is not implemented in v0.1.");
        throw new NotSupportedException("DNS disable is not implemented in v0.1.");
    }
}
