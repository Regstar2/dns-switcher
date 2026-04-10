using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class DnsBenchmarkServiceTests
{
    [Fact]
    public async Task BenchmarkProfilesAsync_SelectsFastestHealthyProfile_AndRestoresOriginalState()
    {
        var configuration = AppConfig.CreateDefault() with
        {
            ActiveProfileId = "google",
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var initialStatus = CreateManualStatus("cloudflare", "Wi-Fi", ["1.1.1.1", "1.0.0.1"]);
        var dnsManager = new MutableDnsManager(initialStatus);
        var queryClient = new FakeDnsQueryClient();
        queryClient.SetSuccessfulProfileLatencies("1.1.1.1", ["cloudflare.com", "openai.com"], 22);
        queryClient.SetSuccessfulProfileLatencies("8.8.8.8", ["google.com", "github.com"], 48);

        var dnsTester = new DnsTester(new DnsProfileService(profileStore), dnsManager, queryClient, NullLogger<DnsTester>.Instance);
        var historyStore = new InMemoryDnsBenchmarkHistoryStore();
        var profileActivator = new FakeProfileActivator(dnsManager);
        var service = new DnsBenchmarkService(
            new DnsProfileService(profileStore),
            dnsManager,
            dnsTester,
            profileActivator,
            historyStore,
            new DnsBenchmarkSelector(),
            NullLogger<DnsBenchmarkService>.Instance);

        var result = await service.BenchmarkProfilesAsync();

        Assert.Equal("cloudflare", result.BestProfileId);
        Assert.Equal(2, result.ProfileResults.Count);
        Assert.True(result.RestoreSucceeded);
        Assert.False(result.WasInterrupted);
        Assert.Equal(["cloudflare", "google", "__benchmark_restore__"], profileActivator.AppliedProfileIds);
        Assert.Equal(["1.1.1.1", "1.0.0.1"], dnsManager.CurrentStatus.Ipv4.NameServers);
        Assert.Equal("google", (await profileStore.LoadAsync()).ActiveProfileId);
        Assert.Single(await historyStore.LoadAsync());
    }

    [Fact]
    public async Task BenchmarkProfilesAsync_ContinuesAfterNonFatalProfileFailure()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var initialStatus = CreateManualStatus("cloudflare", "Wi-Fi", ["1.1.1.1"]);
        var dnsManager = new MutableDnsManager(initialStatus);
        var queryClient = new FakeDnsQueryClient();
        queryClient.SetSuccessfulProfileLatencies("1.1.1.1", ["cloudflare.com", "openai.com"], 25);

        var dnsTester = new DnsTester(new DnsProfileService(profileStore), dnsManager, queryClient, NullLogger<DnsTester>.Instance);
        var historyStore = new InMemoryDnsBenchmarkHistoryStore();
        var profileActivator = new FakeProfileActivator(dnsManager)
        {
            ProfilesThatFail =
            {
                ["google"] = new DnsOperationFailedException("Google benchmark failed."),
            },
        };
        var service = new DnsBenchmarkService(
            new DnsProfileService(profileStore),
            dnsManager,
            dnsTester,
            profileActivator,
            historyStore,
            new DnsBenchmarkSelector(),
            NullLogger<DnsBenchmarkService>.Instance);

        var result = await service.BenchmarkProfilesAsync();

        var failedProfile = Assert.Single(result.ProfileResults, profile => profile.ProfileId == "google");
        Assert.Equal(DnsTestStatus.Failed, failedProfile.TestResult.Status);
        Assert.Contains("Google benchmark failed.", failedProfile.TestResult.Details);
        Assert.Equal("cloudflare", result.BestProfileId);
        Assert.False(result.WasInterrupted);
        Assert.True(result.RestoreSucceeded);
    }

    private static DnsStatus CreateManualStatus(string matchedProfileId, string adapterName, IReadOnlyList<string> ipv4Servers)
    {
        return new DnsStatus(
            IsManaged: true,
            MatchedProfileId: matchedProfileId,
            AdapterName: adapterName,
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, [.. ipv4Servers]),
            Ipv6: new DnsServerState(DnsMode.Dhcp, []),
            Details: string.Empty);
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

    private sealed class MutableDnsManager(DnsStatus initialStatus) : IDnsManager
    {
        public DnsStatus CurrentStatus { get; set; } = initialStatus;

        public Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentStatus);
        }

        public Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            CurrentStatus = new DnsStatus(
                IsManaged: profile.Id != "__benchmark_restore__",
                MatchedProfileId: profile.Id == "__benchmark_restore__" ? null : profile.Id,
                AdapterName: CurrentStatus.AdapterName,
                Mode: profile.Mode == ProfileMode.Dhcp ? DnsMode.Dhcp : DnsMode.Manual,
                Ipv4: profile.Mode == ProfileMode.Dhcp
                    ? new DnsServerState(DnsMode.Dhcp, [])
                    : new DnsServerState(profile.Ipv4.Count == 0 ? DnsMode.Dhcp : DnsMode.Manual, [.. profile.Ipv4]),
                Ipv6: profile.Mode == ProfileMode.Dhcp
                    ? new DnsServerState(DnsMode.Dhcp, [])
                    : new DnsServerState(profile.Ipv6.Count == 0 ? DnsMode.Dhcp : DnsMode.Manual, [.. profile.Ipv6]),
                Details: string.Empty);
            return Task.CompletedTask;
        }

        public Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            CurrentStatus = new DnsStatus(
                IsManaged: false,
                MatchedProfileId: null,
                AdapterName: CurrentStatus.AdapterName,
                Mode: DnsMode.Dhcp,
                Ipv4: new DnsServerState(DnsMode.Dhcp, []),
                Ipv6: new DnsServerState(DnsMode.Dhcp, []),
                Details: string.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileActivator(MutableDnsManager dnsManager) : IDnsProfileActivator
    {
        public Dictionary<string, Exception> ProfilesThatFail { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> AppliedProfileIds { get; } = [];

        public Task ApplyTransientProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            AppliedProfileIds.Add(profile.Id);

            if (ProfilesThatFail.TryGetValue(profile.Id, out var exception))
            {
                throw exception;
            }

            return dnsManager.ApplyProfileAsync(profile, adapterIdOrName, cancellationToken);
        }

        public Task ResetToDhcpTransientAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            AppliedProfileIds.Add("__dhcp__");
            return dnsManager.ResetToDhcpAsync(adapterIdOrName, cancellationToken);
        }
    }

    private sealed class FakeDnsQueryClient : IDnsQueryClient
    {
        private readonly Dictionary<(string Server, string Domain), Queue<DnsQueryProbeResult>> resultsByServerAndDomain = new();

        public Task<DnsQueryProbeResult> QueryAsync(
            string serverAddress,
            string domain,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var key = (serverAddress, domain);

            if (resultsByServerAndDomain.TryGetValue(key, out var results) && results.Count > 0)
            {
                return Task.FromResult(results.Dequeue());
            }

            return Task.FromResult(new DnsQueryProbeResult(
                Success: true,
                ServerAddress: serverAddress,
                Latency: TimeSpan.FromMilliseconds(50),
                AnswerCount: 1,
                Details: "Resolved."));
        }

        public void SetSuccessfulProfileLatencies(string serverAddress, IEnumerable<string> domains, double baseLatencyMs)
        {
            foreach (var domain in domains)
            {
                resultsByServerAndDomain[(serverAddress, domain)] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe(serverAddress, baseLatencyMs),
                    CreateSuccessfulProbe(serverAddress, baseLatencyMs + 2),
                    CreateSuccessfulProbe(serverAddress, baseLatencyMs + 4),
                ]);
            }
        }

        private static DnsQueryProbeResult CreateSuccessfulProbe(string serverAddress, double latencyMs)
        {
            return new DnsQueryProbeResult(
                Success: true,
                ServerAddress: serverAddress,
                Latency: TimeSpan.FromMilliseconds(latencyMs),
                AnswerCount: 1,
                Details: "Resolved.");
        }
    }

    private sealed class InMemoryDnsBenchmarkHistoryStore : IDnsBenchmarkHistoryStore
    {
        private readonly List<DnsBenchmarkResult> entries = [];

        public Task<IReadOnlyList<DnsBenchmarkResult>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DnsBenchmarkResult>>(entries.ToArray());
        }

        public Task AppendAsync(DnsBenchmarkResult result, CancellationToken cancellationToken = default)
        {
            entries.Insert(0, result);
            return Task.CompletedTask;
        }
    }
}
