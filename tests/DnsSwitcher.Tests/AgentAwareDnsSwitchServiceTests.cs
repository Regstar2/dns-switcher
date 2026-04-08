using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Agent;

namespace DnsSwitcher.Tests;

public sealed class AgentAwareDnsSwitchServiceTests
{
    [Fact]
    public async Task ApplyProfileAsync_UsesAgent_WhenAgentIsAvailable()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault());
        var dnsManager = new FakeDnsManager();
        var directSwitchService = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);
        var agentClient = new FakeAgentClient { IsAvailable = true };
        var service = new AgentAwareDnsSwitchService(new DnsProfileService(profileStore), directSwitchService, agentClient);

        await service.ApplyProfileAsync("google");

        Assert.Equal("google", agentClient.AppliedProfile?.Id);
        Assert.Null(dnsManager.AppliedProfile);
        Assert.Equal("google", (await profileStore.LoadAsync()).ActiveProfileId);
    }

    [Fact]
    public async Task ResetToDhcpAsync_UsesAgent_WhenAgentIsAvailable()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault() with { ActiveProfileId = "google" });
        var dnsManager = new FakeDnsManager();
        var directSwitchService = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);
        var agentClient = new FakeAgentClient { IsAvailable = true };
        var service = new AgentAwareDnsSwitchService(new DnsProfileService(profileStore), directSwitchService, agentClient);

        await service.ResetToDhcpAsync();

        Assert.True(agentClient.ResetToDhcpWasCalled);
        Assert.False(dnsManager.ResetToDhcpWasCalled);
        Assert.Null((await profileStore.LoadAsync()).ActiveProfileId);
    }

    [Fact]
    public async Task ApplyProfileAsync_Throws_WhenAgentIsUnavailable_AndDirectFallbackIsDisabled()
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault());
        var dnsManager = new FakeDnsManager();
        var directSwitchService = new DnsSwitchService(new DnsProfileService(profileStore), dnsManager);
        var agentClient = new FakeAgentClient { IsAvailable = false };
        var service = new AgentAwareDnsSwitchService(new DnsProfileService(profileStore), directSwitchService, agentClient);

        await Assert.ThrowsAsync<DnsAgentUnavailableException>(() =>
            service.ApplyProfileAsync("google", allowDirectFallback: false));

        Assert.Null(dnsManager.AppliedProfile);
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

        public Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
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

        public Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            AppliedProfile = profile;
            return Task.CompletedTask;
        }

        public Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            ResetToDhcpWasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAgentClient : IDnsAgentClient
    {
        public bool IsAvailable { get; init; }

        public DnsProfile? AppliedProfile { get; private set; }

        public bool ResetToDhcpWasCalled { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsAvailable);
        }

        public Task ApplyProfileAsync(DnsProfile profile, string? adapterSelection = null, CancellationToken cancellationToken = default)
        {
            AppliedProfile = profile;
            return Task.CompletedTask;
        }

        public Task ResetToDhcpAsync(string? adapterSelection = null, CancellationToken cancellationToken = default)
        {
            ResetToDhcpWasCalled = true;
            return Task.CompletedTask;
        }
    }
}
