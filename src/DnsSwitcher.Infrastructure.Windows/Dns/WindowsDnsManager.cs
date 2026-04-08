using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

public sealed class WindowsDnsManager(
    NetworkAdapterService networkAdapterService,
    ILogger<WindowsDnsManager> logger) : IDnsManager
{
    public async Task<DnsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var selectedAdapter = await networkAdapterService.GetDefaultAdapterAsync(cancellationToken).ConfigureAwait(false);

        var status = new DnsStatus(
            IsManaged: false,
            ActiveProfileId: null,
            AdapterName: selectedAdapter?.Name,
            NameServers: [],
            Details: selectedAdapter is null
                ? "No suitable network adapter was selected."
                : $"Selected adapter '{selectedAdapter.Name}'. System DNS inspection is not implemented in v0.3.");

        return status;
    }

    public Task ApplyProfileAsync(DnsProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        logger.LogWarning("DNS profile apply is not implemented in v0.3. Profile: {ProfileId}", profile.Id);
        throw new NotSupportedException("DNS profile apply is not implemented in v0.3.");
    }

    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("DNS disable is not implemented in v0.3.");
        throw new NotSupportedException("DNS disable is not implemented in v0.3.");
    }
}
