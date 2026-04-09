using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsTester(
    DnsProfileService profileService,
    IDnsManager dnsManager,
    IDnsQueryClient dnsQueryClient)
{
    private const int AttemptCount = 3;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(600);
    private static readonly string[] FallbackDomains =
    [
        "cloudflare.com",
        "github.com",
        "openai.com",
    ];

    public async Task<DnsTestResult> TestCurrentDnsAsync(
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var activeProfile = await profileService.GetActiveProfileAsync(cancellationToken).ConfigureAwait(false);
        var status = await dnsManager.GetStatusAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);

        var selectedProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? activeProfile;

        var domains = ResolveDomains(configuration, selectedProfile);
        var dnsServers = ResolveNameServers(status);

        if (dnsServers.Count == 0)
        {
            return new DnsTestResult(
                AdapterName: status.AdapterName,
                ProfileId: selectedProfile?.Id,
                ProfileName: selectedProfile?.Name,
                DnsServers: [],
                Domains: domains,
                DomainResults: [],
                Status: DnsTestStatus.Failed,
                AverageLatency: null,
                Details: "No DNS servers are configured for the selected adapter.");
        }

        var domainResults = new List<DnsDomainTestResult>(domains.Count);

        foreach (var domain in domains)
        {
            domainResults.Add(await TestDomainAsync(dnsServers, domain, cancellationToken).ConfigureAwait(false));
        }

        var averageLatency = CalculateAverageLatency(domainResults);
        var overallStatus = ResolveOverallStatus(domainResults);
        var details = BuildResultDetails(dnsServers, domains, domainResults, overallStatus, averageLatency);

        return new DnsTestResult(
            AdapterName: status.AdapterName,
            ProfileId: selectedProfile?.Id,
            ProfileName: selectedProfile?.Name,
            DnsServers: dnsServers,
            Domains: domains,
            DomainResults: domainResults,
            Status: overallStatus,
            AverageLatency: averageLatency,
            Details: details);
    }

    private async Task<DnsDomainTestResult> TestDomainAsync(
        IReadOnlyList<string> dnsServers,
        string domain,
        CancellationToken cancellationToken)
    {
        var successfulLatencies = new List<TimeSpan>(AttemptCount);
        string? lastFailureDetails = null;

        for (var attemptIndex = 0; attemptIndex < AttemptCount; attemptIndex++)
        {
            var probeResult = await ProbeDomainAsync(dnsServers, domain, cancellationToken).ConfigureAwait(false);

            if (probeResult.Success)
            {
                successfulLatencies.Add(probeResult.Latency);
            }
            else
            {
                lastFailureDetails = probeResult.Details;
            }
        }

        if (successfulLatencies.Count == 0)
        {
            return new DnsDomainTestResult(
                Domain: domain,
                Status: DnsTestStatus.Failed,
                SuccessfulAttempts: 0,
                TotalAttempts: AttemptCount,
                AverageLatency: null,
                BestLatency: null,
                Details: lastFailureDetails ?? "All DNS resolution attempts failed.");
        }

        var averageLatency = TimeSpan.FromMilliseconds(successfulLatencies.Average(latency => latency.TotalMilliseconds));
        var bestLatency = TimeSpan.FromMilliseconds(successfulLatencies.Min(latency => latency.TotalMilliseconds));
        var status = successfulLatencies.Count < AttemptCount || averageLatency > SlowThreshold
            ? DnsTestStatus.Slow
            : DnsTestStatus.Ok;

        var details = status == DnsTestStatus.Ok
            ? $"Resolved in {successfulLatencies.Count}/{AttemptCount} attempts."
            : successfulLatencies.Count < AttemptCount
                ? $"Resolved in {successfulLatencies.Count}/{AttemptCount} attempts. Last failure: {lastFailureDetails ?? "n/a"}"
                : $"Resolved, but average latency {FormatLatency(averageLatency)} exceeded slow threshold {FormatLatency(SlowThreshold)}.";

        return new DnsDomainTestResult(
            Domain: domain,
            Status: status,
            SuccessfulAttempts: successfulLatencies.Count,
            TotalAttempts: AttemptCount,
            AverageLatency: averageLatency,
            BestLatency: bestLatency,
            Details: details);
    }

    private async Task<DnsQueryProbeResult> ProbeDomainAsync(
        IReadOnlyList<string> dnsServers,
        string domain,
        CancellationToken cancellationToken)
    {
        string? lastFailure = null;

        foreach (var server in dnsServers)
        {
            var probeResult = await dnsQueryClient
                .QueryAsync(server, domain, QueryTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (probeResult.Success)
            {
                return probeResult;
            }

            lastFailure = probeResult.Details;
        }

        return new DnsQueryProbeResult(
            Success: false,
            ServerAddress: dnsServers.First(),
            Latency: TimeSpan.Zero,
            AnswerCount: 0,
            Details: lastFailure ?? "No DNS server returned a successful response.");
    }

    private static IReadOnlyList<string> ResolveDomains(AppConfig configuration, DnsProfile? selectedProfile)
    {
        var fromSelectedProfile = selectedProfile?.TestDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(NormalizeDomain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (fromSelectedProfile is { Length: > 0 })
        {
            return fromSelectedProfile;
        }

        var fromConfiguration = configuration.Profiles
            .SelectMany(profile => profile.TestDomains)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(NormalizeDomain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return fromConfiguration.Length > 0 ? fromConfiguration : FallbackDomains;
    }

    private static IReadOnlyList<string> ResolveNameServers(DnsStatus status)
    {
        return status.Ipv4.NameServers
            .Concat(status.Ipv6.NameServers)
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DnsTestStatus ResolveOverallStatus(IReadOnlyList<DnsDomainTestResult> domainResults)
    {
        if (domainResults.Any(result => result.Status == DnsTestStatus.Failed))
        {
            return DnsTestStatus.Failed;
        }

        return domainResults.Any(result => result.Status == DnsTestStatus.Slow)
            ? DnsTestStatus.Slow
            : DnsTestStatus.Ok;
    }

    private static TimeSpan? CalculateAverageLatency(IReadOnlyList<DnsDomainTestResult> domainResults)
    {
        var averages = domainResults
            .Where(result => result.AverageLatency is not null)
            .Select(result => result.AverageLatency!.Value.TotalMilliseconds)
            .ToArray();

        return averages.Length == 0
            ? null
            : TimeSpan.FromMilliseconds(averages.Average());
    }

    private static string BuildResultDetails(
        IReadOnlyList<string> dnsServers,
        IReadOnlyList<string> domains,
        IReadOnlyList<DnsDomainTestResult> domainResults,
        DnsTestStatus overallStatus,
        TimeSpan? averageLatency)
    {
        var okCount = domainResults.Count(result => result.Status == DnsTestStatus.Ok);
        var slowCount = domainResults.Count(result => result.Status == DnsTestStatus.Slow);
        var failedCount = domainResults.Count(result => result.Status == DnsTestStatus.Failed);

        return
            $"Status {overallStatus}. " +
            $"Servers: {dnsServers.Count}. Domains: {domains.Count}. " +
            $"OK: {okCount}, Slow: {slowCount}, Failed: {failedCount}. " +
            $"Average latency: {(averageLatency is null ? "n/a" : FormatLatency(averageLatency.Value))}.";
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimEnd('.');
    }

    private static string FormatLatency(TimeSpan latency)
    {
        return $"{Math.Round(latency.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }
}
