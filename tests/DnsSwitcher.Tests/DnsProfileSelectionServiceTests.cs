using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class DnsProfileSelectionServiceTests
{
    private readonly DnsProfileSelectionService service = new();

    [Fact]
    public void GetSwitchableProfiles_ExcludesDhcpProfiles()
    {
        var configuration = AppConfig.CreateDefault();

        var profiles = service.GetSwitchableProfiles(configuration);

        Assert.Equal(2, profiles.Count);
        Assert.DoesNotContain(profiles, profile => profile.Mode == ProfileMode.Dhcp);
    }

    [Fact]
    public void GetProfileToEnable_PrefersMatchedProfile()
    {
        var configuration = AppConfig.CreateDefault() with
        {
            ActiveProfileId = "cloudflare",
        };

        var status = CreateStatus(matchedProfileId: "google", mode: DnsMode.Manual);

        var profile = service.GetProfileToEnable(configuration, status, preferredProfileId: "cloudflare");

        Assert.NotNull(profile);
        Assert.Equal("google", profile.Id);
    }

    [Fact]
    public void GetProfileToEnable_FallsBackToFirstStaticProfile()
    {
        var configuration = AppConfig.CreateDefault();
        var status = CreateStatus(matchedProfileId: null, mode: DnsMode.Dhcp);

        var profile = service.GetProfileToEnable(configuration, status, preferredProfileId: null);

        Assert.NotNull(profile);
        Assert.Equal("cloudflare", profile.Id);
    }

    [Fact]
    public void GetNextProfile_ReturnsNextProfileAfterMatchedProfile()
    {
        var configuration = AppConfig.CreateDefault();
        var status = CreateStatus(matchedProfileId: "cloudflare", mode: DnsMode.Manual);

        var profile = service.GetNextProfile(configuration, status, preferredProfileId: null);

        Assert.NotNull(profile);
        Assert.Equal("google", profile.Id);
    }

    [Fact]
    public void GetNextProfile_WrapsAroundToFirstProfile()
    {
        var configuration = AppConfig.CreateDefault();
        var status = CreateStatus(matchedProfileId: "google", mode: DnsMode.Manual);

        var profile = service.GetNextProfile(configuration, status, preferredProfileId: null);

        Assert.NotNull(profile);
        Assert.Equal("cloudflare", profile.Id);
    }

    private static DnsStatus CreateStatus(string? matchedProfileId, DnsMode mode)
    {
        return new DnsStatus(
            IsManaged: matchedProfileId is not null,
            MatchedProfileId: matchedProfileId,
            AdapterName: "Wi-Fi",
            Mode: mode,
            Ipv4: new DnsServerState(mode, []),
            Ipv6: new DnsServerState(mode, []),
            Details: string.Empty);
    }
}
