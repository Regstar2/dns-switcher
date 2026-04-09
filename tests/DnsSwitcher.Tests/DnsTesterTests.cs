using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class DnsTesterTests
{
    [Fact]
    public async Task TestCurrentDnsAsync_UsesMatchedProfileDomains_WhenMatchedProfileExists()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(new DnsStatus(
            IsManaged: true,
            MatchedProfileId: "google",
            AdapterName: "Wi-Fi",
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, ["8.8.8.8"]),
            Ipv6: new DnsServerState(DnsMode.Manual, []),
            Details: string.Empty));
        var queryClient = new FakeDnsQueryClient
        {
            ResultsByDomain =
            {
                ["google.com"] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe("8.8.8.8", 30),
                    CreateSuccessfulProbe("8.8.8.8", 32),
                    CreateSuccessfulProbe("8.8.8.8", 28),
                ]),
                ["github.com"] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe("8.8.8.8", 25),
                    CreateSuccessfulProbe("8.8.8.8", 27),
                    CreateSuccessfulProbe("8.8.8.8", 29),
                ]),
            },
        };
        var tester = new DnsTester(new DnsProfileService(profileStore), dnsManager, queryClient, NullLogger<DnsTester>.Instance);

        var result = await tester.TestCurrentDnsAsync();

        Assert.Equal(DnsTestStatus.Ok, result.Status);
        Assert.Equal(["google.com", "github.com"], result.Domains);
        Assert.All(result.DomainResults, domainResult => Assert.Equal(DnsTestStatus.Ok, domainResult.Status));
    }

    [Fact]
    public async Task TestCurrentDnsAsync_FallsBackToAllConfiguredDomains_WhenNoMatchedOrActiveProfileExists()
    {
        var configuration = AppConfig.CreateDefault() with
        {
            ActiveProfileId = null,
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(new DnsStatus(
            IsManaged: false,
            MatchedProfileId: null,
            AdapterName: "Wi-Fi",
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, ["1.1.1.1"]),
            Ipv6: new DnsServerState(DnsMode.Manual, []),
            Details: string.Empty));
        var queryClient = new FakeDnsQueryClient();
        queryClient.SetAllDomainsToSuccess(["cloudflare.com", "openai.com", "google.com", "github.com"], "1.1.1.1", 40);
        var tester = new DnsTester(new DnsProfileService(profileStore), dnsManager, queryClient, NullLogger<DnsTester>.Instance);

        var result = await tester.TestCurrentDnsAsync();

        Assert.Equal(4, result.Domains.Count);
        Assert.Contains("cloudflare.com", result.Domains);
        Assert.Contains("google.com", result.Domains);
    }

    [Fact]
    public async Task TestCurrentDnsAsync_ReturnsSlow_WhenSomeAttemptsFail()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(new DnsStatus(
            IsManaged: true,
            MatchedProfileId: "cloudflare",
            AdapterName: "Wi-Fi",
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, ["1.1.1.1"]),
            Ipv6: new DnsServerState(DnsMode.Manual, []),
            Details: string.Empty));
        var queryClient = new FakeDnsQueryClient
        {
            ResultsByDomain =
            {
                ["cloudflare.com"] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe("1.1.1.1", 40),
                    CreateFailedProbe("1.1.1.1", "DNS query timed out."),
                    CreateSuccessfulProbe("1.1.1.1", 42),
                ]),
                ["openai.com"] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe("1.1.1.1", 45),
                    CreateSuccessfulProbe("1.1.1.1", 44),
                    CreateSuccessfulProbe("1.1.1.1", 46),
                ]),
            },
        };
        var tester = new DnsTester(new DnsProfileService(profileStore), dnsManager, queryClient, NullLogger<DnsTester>.Instance);

        var result = await tester.TestCurrentDnsAsync();

        Assert.Equal(DnsTestStatus.Slow, result.Status);
        Assert.Contains(result.DomainResults, domainResult => domainResult.Status == DnsTestStatus.Slow);
    }

    [Fact]
    public async Task TestCurrentDnsAsync_ReturnsFailed_WhenNoDnsServersAreConfigured()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(new DnsStatus(
            IsManaged: false,
            MatchedProfileId: null,
            AdapterName: "Wi-Fi",
            Mode: DnsMode.Dhcp,
            Ipv4: new DnsServerState(DnsMode.Dhcp, []),
            Ipv6: new DnsServerState(DnsMode.Dhcp, []),
            Details: string.Empty));
        var tester = new DnsTester(new DnsProfileService(profileStore), dnsManager, new FakeDnsQueryClient(), NullLogger<DnsTester>.Instance);

        var result = await tester.TestCurrentDnsAsync();

        Assert.Equal(DnsTestStatus.Failed, result.Status);
        Assert.Empty(result.DomainResults);
        Assert.Contains("No DNS servers", result.Details);
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

    private static DnsQueryProbeResult CreateFailedProbe(string serverAddress, string details)
    {
        return new DnsQueryProbeResult(
            Success: false,
            ServerAddress: serverAddress,
            Latency: TimeSpan.Zero,
            AnswerCount: 0,
            Details: details);
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

    private sealed class FakeDnsManager(DnsStatus status) : IDnsManager
    {
        public Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(status);
        }

        public Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDnsQueryClient : IDnsQueryClient
    {
        public Dictionary<string, Queue<DnsQueryProbeResult>> ResultsByDomain { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<DnsQueryProbeResult> QueryAsync(
            string serverAddress,
            string domain,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (!ResultsByDomain.TryGetValue(domain, out var results) || results.Count == 0)
            {
                return Task.FromResult(CreateSuccessfulProbe(serverAddress, 50));
            }

            return Task.FromResult(results.Dequeue());
        }

        public void SetAllDomainsToSuccess(IEnumerable<string> domains, string serverAddress, double latencyMs)
        {
            foreach (var domain in domains)
            {
                ResultsByDomain[domain] = new Queue<DnsQueryProbeResult>(
                [
                    CreateSuccessfulProbe(serverAddress, latencyMs),
                    CreateSuccessfulProbe(serverAddress, latencyMs + 2),
                    CreateSuccessfulProbe(serverAddress, latencyMs + 4),
                ]);
            }
        }
    }
}
