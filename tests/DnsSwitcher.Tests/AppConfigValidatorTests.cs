using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class AppConfigValidatorTests
{
    [Fact]
    public void Validate_ReturnsNoErrors_ForDefaultConfig()
    {
        var errors = AppConfigValidator.Validate(AppConfig.CreateDefault());

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsError_ForEmptyProfileName()
    {
        var config = new AppConfig
        {
            Profiles =
            [
                CreateStaticProfile(id: "empty-name", name: " "),
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Code == "EmptyProfileName" && error.Path == "profiles[0].name");
    }

    [Fact]
    public void Validate_ReturnsErrors_ForDuplicateProfileIdsAndNames()
    {
        var config = new AppConfig
        {
            Profiles =
            [
                CreateStaticProfile(id: "duplicate", name: "Same"),
                CreateStaticProfile(id: "DUPLICATE", name: "same"),
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Equal(2, errors.Count(error => error.Code == "DuplicateProfileId"));
        Assert.Equal(2, errors.Count(error => error.Code == "DuplicateProfileName"));
    }

    [Fact]
    public void Validate_ReturnsErrors_ForInvalidIpAddresses()
    {
        var config = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "invalid-ip",
                    Name = "Invalid IP",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["999.1.1.1", "2001:4860:4860::8888"],
                    Ipv6 = ["not-an-ip", "8.8.8.8"],
                },
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Equal(4, errors.Count(error => error.Code == "InvalidIpAddress"));
    }

    [Fact]
    public void Validate_ReturnsError_ForDhcpProfileWithStaticAddresses()
    {
        var config = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "dhcp",
                    Name = "DHCP",
                    Mode = ProfileMode.Dhcp,
                    Ipv4 = ["1.1.1.1"],
                },
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Code == "DhcpProfileHasStaticAddresses");
    }

    [Fact]
    public void Validate_ReturnsError_ForStaticProfileWithoutAddresses()
    {
        var config = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "empty-static",
                    Name = "Empty static",
                    Mode = ProfileMode.Static,
                },
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Code == "StaticProfileWithoutAddresses");
    }

    [Fact]
    public void Validate_ReturnsError_ForUnknownActiveProfile()
    {
        var config = new AppConfig
        {
            ActiveProfileId = "missing",
            Profiles =
            [
                CreateStaticProfile(id: "cloudflare", name: "Cloudflare"),
            ],
        };

        var errors = AppConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Code == "UnknownActiveProfile");
    }

    private static DnsProfile CreateStaticProfile(string id, string name)
    {
        return new DnsProfile
        {
            Id = id,
            Name = name,
            Mode = ProfileMode.Static,
            Ipv4 = ["1.1.1.1"],
        };
    }
}
