using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsProfileSelectionService
{
    public IReadOnlyList<DnsProfile> GetSwitchableProfiles(AppConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.Profiles
            .Where(profile => profile.Mode == ProfileMode.Static)
            .ToArray();
    }

    public DnsProfile? GetProfileToEnable(
        AppConfig configuration,
        DnsStatus status,
        string? preferredProfileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(status);

        var switchableProfiles = GetSwitchableProfiles(configuration);

        return FindById(switchableProfiles, status.MatchedProfileId)
            ?? FindById(switchableProfiles, preferredProfileId)
            ?? FindById(switchableProfiles, configuration.ActiveProfileId)
            ?? switchableProfiles.FirstOrDefault();
    }

    public DnsProfile? GetNextProfile(
        AppConfig configuration,
        DnsStatus status,
        string? preferredProfileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(status);

        var switchableProfiles = GetSwitchableProfiles(configuration);

        if (switchableProfiles.Count == 0)
        {
            return null;
        }

        var currentProfile = FindById(switchableProfiles, status.MatchedProfileId)
            ?? FindById(switchableProfiles, preferredProfileId)
            ?? FindById(switchableProfiles, configuration.ActiveProfileId);

        if (currentProfile is null)
        {
            return switchableProfiles[0];
        }

        var currentIndex = -1;

        for (var index = 0; index < switchableProfiles.Count; index++)
        {
            if (string.Equals(switchableProfiles[index].Id, currentProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return switchableProfiles[0];
        }

        return switchableProfiles[(currentIndex + 1) % switchableProfiles.Count];
    }

    public bool IsSwitchableProfile(AppConfig configuration, string? profileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return FindById(GetSwitchableProfiles(configuration), profileId) is not null;
    }

    private static DnsProfile? FindById(IReadOnlyList<DnsProfile> profiles, string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }
}
