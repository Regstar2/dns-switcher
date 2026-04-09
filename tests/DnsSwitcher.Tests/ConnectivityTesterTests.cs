using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class ConnectivityTesterTests
{
    [Fact]
    public async Task TestCurrentSitesAsync_UsesMatchedProfileUrls_WhenMatchedProfileExists()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: "google"));
        var probeClient = new FakeSiteProbeClient();
        probeClient.SetSuccessfulResults("https://google.com/");
        probeClient.SetSuccessfulResults("https://github.com/");
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, probeClient, NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.Ok, result.Status);
        Assert.Equal(["https://google.com/", "https://github.com/"], result.Urls);
        Assert.All(result.UrlResults, urlResult => Assert.Equal(ConnectivityTestStatus.Ok, urlResult.Status));
    }

    [Fact]
    public async Task TestCurrentSitesAsync_FallsBackToConfiguredUrlUnion_WhenNoMatchedProfileExists()
    {
        var configuration = AppConfig.CreateDefault() with
        {
            ActiveProfileId = "cloudflare",
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: null));
        var probeClient = new FakeSiteProbeClient();
        probeClient.SetSuccessfulResults("https://cloudflare.com/");
        probeClient.SetSuccessfulResults("https://openai.com/");
        probeClient.SetSuccessfulResults("https://google.com/");
        probeClient.SetSuccessfulResults("https://github.com/");
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, probeClient, NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.Ok, result.Status);
        Assert.Equal(4, result.Urls.Count);
        Assert.Contains("https://cloudflare.com/", result.Urls);
        Assert.Contains("https://google.com/", result.Urls);
    }

    [Fact]
    public async Task TestCurrentSitesAsync_ReturnsNotConfigured_WhenNoUrlsExist()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "custom",
                    Name = "Custom",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["1.1.1.1"],
                },
            ],
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: null));
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, new FakeSiteProbeClient(), NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.NotConfigured, result.Status);
        Assert.Empty(result.UrlResults);
        Assert.Contains("No test URLs", result.Details);
    }

    [Fact]
    public async Task TestCurrentSitesAsync_ReturnsSlow_WhenAverageLatencyIsTooHigh()
    {
        var configuration = AppConfig.CreateDefault();
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: "google"));
        var probeClient = new FakeSiteProbeClient();
        probeClient.SetSuccessfulResults("https://google.com/", 2800, 3000);
        probeClient.SetSuccessfulResults("https://github.com/", 2600, 2900);
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, probeClient, NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.Slow, result.Status);
        Assert.All(result.UrlResults, urlResult => Assert.Equal(ConnectivityTestStatus.Slow, urlResult.Status));
    }

    [Fact]
    public async Task TestCurrentSitesAsync_ReturnsBlocked_WhenAllAttemptsLookBlocked()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "blocked",
                    Name = "Blocked",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["9.9.9.9"],
                    TestUrls = ["https://blocked.example/"],
                },
            ],
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: "blocked"));
        var probeClient = new FakeSiteProbeClient
        {
            ResultsByUrl =
            {
                ["https://blocked.example/"] = new Queue<SiteProbeResult>(
                [
                    CreateBlockedProbe("https://blocked.example/", "TCP connect timed out."),
                    CreateBlockedProbe("https://blocked.example/", "TCP connect timed out."),
                ]),
            },
        };
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, probeClient, NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.Blocked, result.Status);
        Assert.Single(result.UrlResults);
        Assert.Equal(ConnectivityTestStatus.Blocked, result.UrlResults[0].Status);
    }

    [Fact]
    public async Task TestCurrentSitesAsync_ReturnsFailed_WhenUrlIsInvalid()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "invalid",
                    Name = "Invalid",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["8.8.8.8"],
                    TestUrls = ["ftp://example.com/"],
                },
            ],
        };
        var profileStore = new InMemoryProfileStore(configuration);
        var dnsManager = new FakeDnsManager(CreateStatus(matchedProfileId: "invalid"));
        var tester = new ConnectivityTester(new DnsProfileService(profileStore), dnsManager, new FakeSiteProbeClient(), NullLogger<ConnectivityTester>.Instance);

        var result = await tester.TestCurrentSitesAsync();

        Assert.Equal(ConnectivityTestStatus.Failed, result.Status);
        Assert.Single(result.UrlResults);
        Assert.Equal(ConnectivityTestStatus.Failed, result.UrlResults[0].Status);
        Assert.Contains("invalid or unsupported", result.UrlResults[0].Details, StringComparison.OrdinalIgnoreCase);
    }

    private static DnsStatus CreateStatus(string? matchedProfileId)
    {
        return new DnsStatus(
            IsManaged: matchedProfileId is not null,
            MatchedProfileId: matchedProfileId,
            AdapterName: "Wi-Fi",
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, ["1.1.1.1"]),
            Ipv6: new DnsServerState(DnsMode.Manual, []),
            Details: string.Empty);
    }

    private static SiteProbeResult CreateSuccessfulProbe(string url, double latencyMs)
    {
        var latency = TimeSpan.FromMilliseconds(latencyMs);

        return new SiteProbeResult(
            Url: url,
            Success: true,
            IsBlockedIndicator: false,
            Dns: new SiteStageResult(true, TimeSpan.FromMilliseconds(20), "Resolved 1 address(es)."),
            Connect: new SiteStageResult(true, TimeSpan.FromMilliseconds(30), "Connected."),
            Tls: new SiteStageResult(true, TimeSpan.FromMilliseconds(40), "TLS established."),
            Http: new SiteStageResult(true, latency, "HTTP 200 received via HEAD."),
            HttpStatusCode: 200,
            HttpMethod: "HEAD",
            TotalLatency: latency,
            Details: "HTTP 200 received via HEAD.");
    }

    private static SiteProbeResult CreateBlockedProbe(string url, string details)
    {
        return new SiteProbeResult(
            Url: url,
            Success: false,
            IsBlockedIndicator: true,
            Dns: new SiteStageResult(true, TimeSpan.FromMilliseconds(20), "Resolved 1 address(es)."),
            Connect: new SiteStageResult(false, TimeSpan.FromMilliseconds(2000), details),
            Tls: new SiteStageResult(false, null, "Not attempted."),
            Http: new SiteStageResult(false, null, "Not attempted."),
            HttpStatusCode: null,
            HttpMethod: "HEAD",
            TotalLatency: TimeSpan.FromMilliseconds(2000),
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

    private sealed class FakeSiteProbeClient : ISiteProbeClient
    {
        public Dictionary<string, Queue<SiteProbeResult>> ResultsByUrl { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<SiteProbeResult> ProbeAsync(
            Uri url,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var key = url.ToString();

            if (!ResultsByUrl.TryGetValue(key, out var results) || results.Count == 0)
            {
                return Task.FromResult(CreateSuccessfulProbe(key, 120));
            }

            return Task.FromResult(results.Dequeue());
        }

        public void SetSuccessfulResults(string url, double firstLatencyMs = 150, double secondLatencyMs = 170)
        {
            ResultsByUrl[url] = new Queue<SiteProbeResult>(
            [
                CreateSuccessfulProbe(url, firstLatencyMs),
                CreateSuccessfulProbe(url, secondLatencyMs),
            ]);
        }
    }
}
