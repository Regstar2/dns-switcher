using System.Net;
using System.Net.Sockets;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public static class AppConfigValidator
{
    public static IReadOnlyList<ValidationError> Validate(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ValidationError>();

        if (config.Version != AppConfig.CurrentVersion)
        {
            errors.Add(new ValidationError(
                "UnsupportedVersion",
                "version",
                $"Unsupported config version '{config.Version}'. Expected '{AppConfig.CurrentVersion}'."));
        }

        ValidateProfiles(config, errors);
        ValidateActiveProfile(config, errors);

        return errors;
    }

    private static void ValidateProfiles(AppConfig config, List<ValidationError> errors)
    {
        for (var index = 0; index < config.Profiles.Count; index++)
        {
            var profile = config.Profiles[index];
            var profilePath = $"profiles[{index}]";

            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                errors.Add(new ValidationError("EmptyProfileId", $"{profilePath}.id", "Profile id must not be empty."));
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                errors.Add(new ValidationError("EmptyProfileName", $"{profilePath}.name", "Profile name must not be empty."));
            }

            ValidateMode(profile, profilePath, errors);
            ValidateAddresses(profile.Ipv4, AddressFamily.InterNetwork, $"{profilePath}.ipv4", errors);
            ValidateAddresses(profile.Ipv6, AddressFamily.InterNetworkV6, $"{profilePath}.ipv6", errors);
        }

        AddDuplicateErrors(
            config.Profiles,
            profile => profile.Id,
            "DuplicateProfileId",
            "id",
            "Duplicate profile id.",
            errors);

        AddDuplicateErrors(
            config.Profiles,
            profile => profile.Name,
            "DuplicateProfileName",
            "name",
            "Duplicate profile name.",
            errors);
    }

    private static void ValidateMode(DnsProfile profile, string profilePath, List<ValidationError> errors)
    {
        var hasStaticAddresses = profile.Ipv4.Count > 0 || profile.Ipv6.Count > 0;

        if (!Enum.IsDefined(profile.Mode))
        {
            errors.Add(new ValidationError("InvalidProfileMode", $"{profilePath}.mode", $"Unknown profile mode '{profile.Mode}'."));
            return;
        }

        if (profile.Mode == ProfileMode.Dhcp && hasStaticAddresses)
        {
            errors.Add(new ValidationError(
                "DhcpProfileHasStaticAddresses",
                $"{profilePath}.mode",
                "DHCP profile must not contain static DNS addresses."));
        }

        if (profile.Mode == ProfileMode.Static && !hasStaticAddresses)
        {
            errors.Add(new ValidationError(
                "StaticProfileWithoutAddresses",
                $"{profilePath}.mode",
                "Static profile must contain at least one DNS address."));
        }
    }

    private static void ValidateAddresses(
        IReadOnlyList<string> addresses,
        AddressFamily expectedFamily,
        string path,
        List<ValidationError> errors)
    {
        for (var index = 0; index < addresses.Count; index++)
        {
            var address = addresses[index];

            if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != expectedFamily)
            {
                errors.Add(new ValidationError(
                    "InvalidIpAddress",
                    $"{path}[{index}]",
                    $"Invalid {(expectedFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")} address '{address}'."));
            }
        }
    }

    private static void ValidateActiveProfile(AppConfig config, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(config.ActiveProfileId))
        {
            return;
        }

        var exists = config.Profiles.Any(profile =>
            string.Equals(profile.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            errors.Add(new ValidationError(
                "UnknownActiveProfile",
                "activeProfileId",
                $"Active profile '{config.ActiveProfileId}' does not exist."));
        }
    }

    private static void AddDuplicateErrors(
        IReadOnlyList<DnsProfile> profiles,
        Func<DnsProfile, string> selector,
        string code,
        string propertyName,
        string message,
        List<ValidationError> errors)
    {
        var groups = profiles
            .Select((profile, index) => new { Value = selector(profile), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            foreach (var item in group)
            {
                errors.Add(new ValidationError(code, $"profiles[{item.Index}].{propertyName}", message));
            }
        }
    }
}
