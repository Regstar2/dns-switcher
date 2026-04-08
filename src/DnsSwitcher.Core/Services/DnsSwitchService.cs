using DnsSwitcher.Core.Abstractions;

namespace DnsSwitcher.Core.Services;

public sealed class DnsSwitchService(DnsProfileService profileService, IDnsManager dnsManager)
{
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

    public async Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        await dnsManager.ResetToDhcpAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
        await profileService.ClearActiveProfileAsync(cancellationToken).ConfigureAwait(false);
    }
}
