using DnsSwitcher.Core.Abstractions;

namespace DnsSwitcher.Core.Services;

public sealed class DnsSwitchService(DnsProfileService profileService, IDnsManager dnsManager)
{
    public async Task ApplyProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var profile = await profileService.GetRequiredProfileAsync(profileId, cancellationToken).ConfigureAwait(false);

        await dnsManager.ApplyProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        await profileService.SetActiveProfileAsync(profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetToDhcpAsync(CancellationToken cancellationToken = default)
    {
        await dnsManager.ResetToDhcpAsync(cancellationToken).ConfigureAwait(false);
        await profileService.ClearActiveProfileAsync(cancellationToken).ConfigureAwait(false);
    }
}
