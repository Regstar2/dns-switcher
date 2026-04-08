using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class DnsSwitchServiceTests
{
    [Fact]
    public async Task ApplyProfileAsync_Throws_WhenProfileDoesNotExist()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault());
        var dnsManager = new FakeDnsManager();
        var service = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);

        var exception = await Assert.ThrowsAsync<DnsProfileNotFoundException>(() => service.ApplyProfileAsync("missing"));

        Assert.Equal("missing", exception.ProfileId);
        Assert.Null(dnsManager.AppliedProfile);
    }

    [Fact]
    public async Task ApplyProfileAsync_AppliesProfile_AndMarksItActive()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault());
        var dnsManager = new FakeDnsManager();
        var service = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);

        await service.ApplyProfileAsync("google");

        Assert.Equal("google", dnsManager.AppliedProfile?.Id);
        Assert.Equal("google", (await profileStore.LoadAsync()).ActiveProfileId);
    }

    [Fact]
    public async Task ResetToDhcpAsync_ResetsDns_AndClearsActiveProfile()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault() with { ActiveProfileId = "google" });
        var dnsManager = new FakeDnsManager();
        var service = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);

        await service.ResetToDhcpAsync();

        Assert.True(dnsManager.ResetToDhcpWasCalled);
        Assert.Null((await profileStore.LoadAsync()).ActiveProfileId);
    }

    private sealed class InMemoryProfileStore(AppConfig configuration) : IProfileStore
    {
        private AppConfig configuration = configuration;

        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(configuration);
        }

        public Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default)
        {
            this.configuration = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDnsManager : IDnsManager
    {
        public DnsProfile? AppliedProfile { get; private set; }

        public bool ResetToDhcpWasCalled { get; private set; }

        public Task<DnsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DnsStatus(
                IsManaged: false,
                MatchedProfileId: null,
                AdapterName: "Wi-Fi",
                Mode: DnsMode.Unknown,
                Ipv4: new DnsServerState(DnsMode.Unknown, []),
                Ipv6: new DnsServerState(DnsMode.Unknown, []),
                Details: string.Empty));
        }

        public Task ApplyProfileAsync(DnsProfile profile, CancellationToken cancellationToken = default)
        {
            AppliedProfile = profile;
            return Task.CompletedTask;
        }

        public Task ResetToDhcpAsync(CancellationToken cancellationToken = default)
        {
            ResetToDhcpWasCalled = true;
            return Task.CompletedTask;
        }
    }
}
