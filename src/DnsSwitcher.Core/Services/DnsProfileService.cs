using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsProfileService(IProfileStore profileStore)
{
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return profileStore.EnsureCreatedAsync(cancellationToken);
    }

    public Task<DnsConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
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
}
