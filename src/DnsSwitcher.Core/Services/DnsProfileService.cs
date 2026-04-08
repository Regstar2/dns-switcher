using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsProfileService(IProfileStore profileStore)
{
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return profileStore.EnsureCreatedAsync(cancellationToken);
    }

    public Task<AppConfig> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return profileStore.LoadAsync(cancellationToken);
    }

    public async Task<DnsProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(configuration.ActiveProfileId))
        {
            return null;
        }

        return configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DnsProfile?> FindProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        return configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DnsProfile> GetRequiredProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(id, cancellationToken).ConfigureAwait(false);
        return profile ?? throw new DnsProfileNotFoundException(id);
    }

    public async Task SetActiveProfileAsync(string? profileId, CancellationToken cancellationToken = default)
    {
        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(profileId)
            && configuration.Profiles.All(profile => !string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DnsProfileNotFoundException(profileId);
        }

        await profileStore
            .SaveAsync(configuration with { ActiveProfileId = profileId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ClearActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        return SetActiveProfileAsync(null, cancellationToken);
    }
}
