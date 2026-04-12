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

    public Task SaveConfigurationAsync(AppConfig configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return profileStore.SaveAsync(configuration, cancellationToken);
    }

    public async Task SaveProfileAsync(
        DnsProfile profile,
        string? previousProfileId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var profiles = configuration.Profiles.ToList();
        var activeProfileId = configuration.ActiveProfileId;

        if (!string.IsNullOrWhiteSpace(previousProfileId)
            && !string.Equals(previousProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            var previousIndex = profiles.FindIndex(existing =>
                string.Equals(existing.Id, previousProfileId, StringComparison.OrdinalIgnoreCase));

            if (previousIndex >= 0)
            {
                profiles.RemoveAt(previousIndex);

                if (string.Equals(activeProfileId, previousProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    activeProfileId = profile.Id;
                }
            }
        }

        var existingIndex = profiles.FindIndex(existing =>
            string.Equals(existing.Id, profile.Id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        await profileStore
            .SaveAsync(configuration with { Profiles = profiles, ActiveProfileId = activeProfileId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var profiles = configuration.Profiles
            .Where(profile => !string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (profiles.Count == configuration.Profiles.Count)
        {
            throw new DnsProfileNotFoundException(profileId);
        }

        var activeProfileId = string.Equals(configuration.ActiveProfileId, profileId, StringComparison.OrdinalIgnoreCase)
            ? null
            : configuration.ActiveProfileId;

        await profileStore
            .SaveAsync(configuration with { Profiles = profiles, ActiveProfileId = activeProfileId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ImportProfilesAsync(
        IReadOnlyList<DnsProfile> importedProfiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importedProfiles);

        if (importedProfiles.Count == 0)
        {
            return 0;
        }

        var configuration = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var profiles = configuration.Profiles.ToList();

        foreach (var importedProfile in importedProfiles)
        {
            var existingIndex = profiles.FindIndex(profile =>
                string.Equals(profile.Id, importedProfile.Id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                profiles[existingIndex] = importedProfile;
            }
            else
            {
                profiles.Add(importedProfile);
            }
        }

        await profileStore
            .SaveAsync(configuration with { Profiles = profiles }, cancellationToken)
            .ConfigureAwait(false);

        return importedProfiles.Count;
    }
}
