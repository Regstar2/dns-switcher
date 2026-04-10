using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsSwitchService(DnsProfileService profileService, IDnsManager dnsManager)
{
    public Task ApplyTransientProfileAsync(
        DnsProfile profile,
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return dnsManager.ApplyProfileAsync(profile, adapterIdOrName, cancellationToken);
    }

    public async Task ApplyProfileAsync(
        string profileId,
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var profile = await profileService.GetRequiredProfileAsync(profileId, cancellationToken).ConfigureAwait(false);

        await dnsManager.ApplyProfileAsync(profile, adapterIdOrName, cancellationToken).ConfigureAwait(false);
        await profileService.SetActiveProfileAsync(profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public Task ResetToDhcpTransientAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        return dnsManager.ResetToDhcpAsync(adapterIdOrName, cancellationToken);
    }

    public async Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        await dnsManager.ResetToDhcpAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
        await profileService.ClearActiveProfileAsync(cancellationToken).ConfigureAwait(false);
    }
}
