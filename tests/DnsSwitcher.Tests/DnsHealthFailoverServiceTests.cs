using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class DnsHealthFailoverServiceTests
{
    [Fact]
    public async Task EvaluateAsync_SwitchesToFallbackProfile_WhenFailureThresholdIsReached()
    {
        var fixture = CreateFixture(new DnsHealthSettings
        {
            Enabled = true,
            FailureThreshold = 2,
            ActionOnFailure = DnsHealthFailureAction.SwitchToFallbackProfile,
            FallbackProfileId = "google",
            TestDomains = ["example.com"],
        });
        fixture.QueryClient.Succeeds = false;

        _ = await fixture.Service.EvaluateAsync();
        var result = await fixture.Service.EvaluateAsync();

        Assert.True(result.SwitchedProfile);
        Assert.Equal("google", result.TargetProfileId);
        Assert.Equal("google", fixture.Activator.AppliedProfileIds.Single());
        Assert.Equal("google", (await fixture.ProfileStore.LoadAsync()).ActiveProfileId);
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotSwitch_WhenNotifyOnlyModeIsEnabled()
    {
        var fixture = CreateFixture(new DnsHealthSettings
        {
            Enabled = true,
            FailureThreshold = 1,
            ActionOnFailure = DnsHealthFailureAction.NotifyOnly,
            TestDomains = ["example.com"],
        });
        fixture.QueryClient.Succeeds = false;

        var result = await fixture.Service.EvaluateAsync();

        Assert.False(result.SwitchedProfile);
        Assert.Empty(fixture.Activator.AppliedProfileIds);
        Assert.Equal(DnsHealthStatus.Failed, result.Status);
        Assert.Contains("Notify-only", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_RespectsCooldownAndPreventsEndlessSwitchLoop()
    {
        var now = new DateTimeOffset(2026, 4, 16, 10, 0, 0, TimeSpan.Zero);
        var fixture = CreateFixture(new DnsHealthSettings
        {
            Enabled = true,
            FailureThreshold = 1,
            CooldownSeconds = 300,
            ActionOnFailure = DnsHealthFailureAction.SwitchToNextProfile,
            FailoverChain = ["cloudflare", "google"],
            TestDomains = ["example.com"],
        }, () => now);
        fixture.QueryClient.Succeeds = false;

        var first = await fixture.Service.EvaluateAsync();
        var second = await fixture.Service.EvaluateAsync();

        Assert.True(first.SwitchedProfile);
        Assert.False(second.SwitchedProfile);
        Assert.Single(fixture.Activator.AppliedProfileIds);
        Assert.Equal(DnsHealthStatus.Cooldown, second.Status);
    }

    [Fact]
    public async Task EvaluateAsync_PersistsSuccessfulState()
    {
        var fixture = CreateFixture(new DnsHealthSettings
        {
            Enabled = true,
            RecoveryThreshold = 1,
            TestDomains = ["example.com"],
        });
        fixture.QueryClient.Succeeds = true;

        var result = await fixture.Service.EvaluateAsync();
        var state = await fixture.StateStore.LoadAsync();

        Assert.Equal(DnsHealthStatus.Healthy, result.Status);
        Assert.Equal(DnsHealthStatus.Healthy, state.Status);
        Assert.NotNull(state.LastCheckedUtc);
        Assert.NotNull(state.LastSuccessfulCheckUtc);
    }

    [Fact]
    public async Task SaveSettingsAsync_NormalizesUnsafeValuesBeforePersistence()
    {
        var fixture = CreateFixture(new DnsHealthSettings());

        await fixture.Service.SaveSettingsAsync(new DnsHealthSettings
        {
            MonitorIntervalSeconds = 1,
            FailureThreshold = 0,
            RecoveryThreshold = -5,
            CooldownSeconds = -10,
            FailoverChain = [" cloudflare ", "cloudflare", "", "google"],
            TestDomains = [" example.com. ", "example.com", ""],
            ExpectedAddresses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [" example.com. "] = [" 203.0.113.1 ", "203.0.113.1", ""],
                [" "] = ["203.0.113.2"],
            },
        });

        var saved = await fixture.SettingsStore.LoadAsync();

        Assert.Equal(15, saved.MonitorIntervalSeconds);
        Assert.Equal(1, saved.FailureThreshold);
        Assert.Equal(1, saved.RecoveryThreshold);
        Assert.Equal(0, saved.CooldownSeconds);
        Assert.Equal(["cloudflare", "google"], saved.FailoverChain);
        Assert.Equal(["example.com"], saved.TestDomains);
        Assert.True(saved.ExpectedAddresses.ContainsKey("example.com"));
        Assert.Equal(["203.0.113.1"], saved.ExpectedAddresses["example.com"]);
    }

    private static Fixture CreateFixture(DnsHealthSettings settings, Func<DateTimeOffset>? clock = null)
    {
        var profileStore = new InMemoryProfileStore(AppConfig.CreateDefault() with { ActiveProfileId = "cloudflare" });
        var profileService = new DnsProfileService(profileStore);
        var dnsManager = new FakeDnsManager();
        var queryClient = new FakeDnsQueryClient();
        var dnsTester = new DnsTester(profileService, dnsManager, queryClient, NullLogger<DnsTester>.Instance);
        var settingsStore = new InMemoryHealthSettingsStore(settings);
        var stateStore = new InMemoryHealthStateStore();
        var activator = new FakeProfileActivator();
        var service = new DnsHealthFailoverService(
            profileService,
            dnsManager,
            dnsTester,
            activator,
            settingsStore,
            stateStore,
            NullLogger<DnsHealthFailoverService>.Instance,
            clock);

        return new Fixture(service, profileStore, settingsStore, stateStore, queryClient, activator);
    }

    private sealed record Fixture(
        DnsHealthFailoverService Service,
        InMemoryProfileStore ProfileStore,
        InMemoryHealthSettingsStore SettingsStore,
        InMemoryHealthStateStore StateStore,
        FakeDnsQueryClient QueryClient,
        FakeProfileActivator Activator);

    private sealed class InMemoryProfileStore(AppConfig configuration) : IProfileStore
    {
        private AppConfig configuration = configuration;

        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(configuration);

        public Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default)
        {
            this.configuration = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryHealthSettingsStore(DnsHealthSettings settings) : IDnsHealthSettingsStore
    {
        private DnsHealthSettings settings = settings;

        public Task<DnsHealthSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(DnsHealthSettings settings, CancellationToken cancellationToken = default)
        {
            this.settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryHealthStateStore : IDnsHealthStateStore
    {
        private DnsHealthState state = new();

        public Task<DnsHealthState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);

        public Task SaveAsync(DnsHealthState state, CancellationToken cancellationToken = default)
        {
            this.state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDnsManager : IDnsManager
    {
        public Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DnsStatus(
                IsManaged: true,
                MatchedProfileId: "cloudflare",
                AdapterName: "Ethernet",
                Mode: DnsMode.Manual,
                Ipv4: new DnsServerState(DnsMode.Manual, ["1.1.1.1"]),
                Ipv6: new DnsServerState(DnsMode.Unknown, []),
                Details: "test"));
        }

        public Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDnsQueryClient : IDnsQueryClient
    {
        public bool Succeeds { get; set; } = true;

        public Task<DnsQueryProbeResult> QueryAsync(
            string serverAddress,
            string domain,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DnsQueryProbeResult(
                Success: Succeeds,
                ServerAddress: serverAddress,
                Latency: TimeSpan.FromMilliseconds(10),
                AnswerCount: Succeeds ? 1 : 0,
                Details: Succeeds ? "Resolved." : "DNS query timed out.",
                AnswerAddresses: Succeeds ? ["203.0.113.1"] : []));
        }
    }

    private sealed class FakeProfileActivator : IDnsProfileActivator
    {
        public List<string> AppliedProfileIds { get; } = [];

        public Task ApplyTransientProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            AppliedProfileIds.Add(profile.Id);
            return Task.CompletedTask;
        }

        public Task ResetToDhcpTransientAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
