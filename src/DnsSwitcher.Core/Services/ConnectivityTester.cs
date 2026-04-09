using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class ConnectivityTester(
    DnsProfileService profileService,
    IDnsManager dnsManager,
    ISiteProbeClient siteProbeClient)
{
    private const int AttemptCount = 2;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(2.5);

    public async Task<ConnectivityTestResult> TestCurrentSitesAsync(
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var status = await dnsManager.GetStatusAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);

        var selectedProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        var urls = ResolveUrls(configuration, selectedProfile);

        if (urls.Count == 0)
        {
            return new ConnectivityTestResult(
                AdapterName: status.AdapterName,
                ProfileId: selectedProfile?.Id,
                ProfileName: selectedProfile?.Name,
                Urls: [],
                UrlResults: [],
                Status: ConnectivityTestStatus.NotConfigured,
                AverageLatency: null,
                Details: "No test URLs are configured for the current profile or configuration.");
        }

        var urlResults = new List<UrlConnectivityTestResult>(urls.Count);

        foreach (var url in urls)
        {
            urlResults.Add(await TestUrlAsync(url, cancellationToken).ConfigureAwait(false));
        }

        var averageLatency = CalculateAverageLatency(urlResults);
        var overallStatus = ResolveOverallStatus(urlResults);

        return new ConnectivityTestResult(
            AdapterName: status.AdapterName,
            ProfileId: selectedProfile?.Id,
            ProfileName: selectedProfile?.Name,
            Urls: urls,
            UrlResults: urlResults,
            Status: overallStatus,
            AverageLatency: averageLatency,
            Details: BuildDetails(urlResults, overallStatus, averageLatency));
    }

    private async Task<UrlConnectivityTestResult> TestUrlAsync(string rawUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return new UrlConnectivityTestResult(
                Url: rawUrl,
                Status: ConnectivityTestStatus.Failed,
                SuccessfulAttempts: 0,
                TotalAttempts: AttemptCount,
                Dns: new SiteStageResult(false, null, "URL is invalid or unsupported."),
                Connect: new SiteStageResult(false, null, "Not attempted."),
                Tls: new SiteStageResult(false, null, "Not attempted."),
                Http: new SiteStageResult(false, null, "Not attempted."),
                HttpStatusCode: null,
                HttpMethod: "HEAD",
                AverageLatency: null,
                BestLatency: null,
                Details: "URL is invalid or unsupported. Only absolute http/https URLs are supported.");
        }

        var attempts = new List<SiteProbeResult>(AttemptCount);

        for (var attemptIndex = 0; attemptIndex < AttemptCount; attemptIndex++)
        {
            attempts.Add(await siteProbeClient.ProbeAsync(uri, ProbeTimeout, cancellationToken).ConfigureAwait(false));
        }

        var successfulAttempts = attempts.Where(attempt => attempt.Success && attempt.HttpStatusCode != 451).ToArray();
        var blockedAttempts = attempts.Where(IsBlockedLike).ToArray();
        var templateAttempt = successfulAttempts.FirstOrDefault() ?? attempts.Last();

        if (successfulAttempts.Length == 0)
        {
            var failedStatus = blockedAttempts.Length > 0
                ? ConnectivityTestStatus.Blocked
                : ConnectivityTestStatus.Failed;

            return new UrlConnectivityTestResult(
                Url: rawUrl,
                Status: failedStatus,
                SuccessfulAttempts: 0,
                TotalAttempts: AttemptCount,
                Dns: templateAttempt.Dns,
                Connect: templateAttempt.Connect,
                Tls: templateAttempt.Tls,
                Http: templateAttempt.Http,
                HttpStatusCode: templateAttempt.HttpStatusCode,
                HttpMethod: templateAttempt.HttpMethod,
                AverageLatency: null,
                BestLatency: null,
                Details: templateAttempt.Details);
        }

        var averageLatency = TimeSpan.FromMilliseconds(successfulAttempts.Average(attempt => attempt.TotalLatency!.Value.TotalMilliseconds));
        var bestLatency = TimeSpan.FromMilliseconds(successfulAttempts.Min(attempt => attempt.TotalLatency!.Value.TotalMilliseconds));
        var status = blockedAttempts.Length > 0 || successfulAttempts.Length < AttemptCount || averageLatency > SlowThreshold
            ? ConnectivityTestStatus.Slow
            : ConnectivityTestStatus.Ok;

        var details = status == ConnectivityTestStatus.Ok
            ? $"Site responded in {successfulAttempts.Length}/{AttemptCount} attempts."
            : blockedAttempts.Length > 0
                ? $"Site responded, but one or more attempts looked blocked. Last details: {attempts.Last().Details}"
                : successfulAttempts.Length < AttemptCount
                    ? $"Site responded in {successfulAttempts.Length}/{AttemptCount} attempts. Last details: {attempts.Last().Details}"
                    : $"Site responded, but average latency {FormatLatency(averageLatency)} exceeded slow threshold {FormatLatency(SlowThreshold)}.";

        var representativeAttempt = successfulAttempts.First();

        return new UrlConnectivityTestResult(
            Url: rawUrl,
            Status: status,
            SuccessfulAttempts: successfulAttempts.Length,
            TotalAttempts: AttemptCount,
            Dns: representativeAttempt.Dns,
            Connect: representativeAttempt.Connect,
            Tls: representativeAttempt.Tls,
            Http: representativeAttempt.Http,
            HttpStatusCode: representativeAttempt.HttpStatusCode,
            HttpMethod: representativeAttempt.HttpMethod,
            AverageLatency: averageLatency,
            BestLatency: bestLatency,
            Details: details);
    }

    private static IReadOnlyList<string> ResolveUrls(AppConfig configuration, DnsProfile? selectedProfile)
    {
        var fromSelectedProfile = selectedProfile?.TestUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (fromSelectedProfile is { Length: > 0 })
        {
            return fromSelectedProfile;
        }

        return configuration.Profiles
            .SelectMany(profile => profile.TestUrls)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ConnectivityTestStatus ResolveOverallStatus(IReadOnlyList<UrlConnectivityTestResult> urlResults)
    {
        if (urlResults.Count == 0)
        {
            return ConnectivityTestStatus.NotConfigured;
        }

        if (urlResults.Any(result => result.Status == ConnectivityTestStatus.Blocked))
        {
            return ConnectivityTestStatus.Blocked;
        }

        if (urlResults.Any(result => result.Status == ConnectivityTestStatus.Failed))
        {
            return ConnectivityTestStatus.Failed;
        }

        return urlResults.Any(result => result.Status == ConnectivityTestStatus.Slow)
            ? ConnectivityTestStatus.Slow
            : ConnectivityTestStatus.Ok;
    }

    private static bool IsBlockedLike(SiteProbeResult attempt)
    {
        if (attempt.HttpStatusCode == 451)
        {
            return true;
        }

        if (!attempt.Dns.Success)
        {
            return false;
        }

        var details = attempt.Details.ToLowerInvariant();

        return details.Contains("timed out", StringComparison.Ordinal)
            || details.Contains("refused", StringComparison.Ordinal)
            || details.Contains("reset", StringComparison.Ordinal)
            || details.Contains("unreachable", StringComparison.Ordinal)
            || details.Contains("legal reasons", StringComparison.Ordinal);
    }

    private static TimeSpan? CalculateAverageLatency(IReadOnlyList<UrlConnectivityTestResult> urlResults)
    {
        var latencies = urlResults
            .Where(result => result.AverageLatency is not null)
            .Select(result => result.AverageLatency!.Value.TotalMilliseconds)
            .ToArray();

        return latencies.Length == 0
            ? null
            : TimeSpan.FromMilliseconds(latencies.Average());
    }

    private static string BuildDetails(
        IReadOnlyList<UrlConnectivityTestResult> urlResults,
        ConnectivityTestStatus overallStatus,
        TimeSpan? averageLatency)
    {
        var okCount = urlResults.Count(result => result.Status == ConnectivityTestStatus.Ok);
        var slowCount = urlResults.Count(result => result.Status == ConnectivityTestStatus.Slow);
        var blockedCount = urlResults.Count(result => result.Status == ConnectivityTestStatus.Blocked);
        var failedCount = urlResults.Count(result => result.Status == ConnectivityTestStatus.Failed);

        return
            $"Status {overallStatus}. " +
            $"URLs: {urlResults.Count}. " +
            $"OK: {okCount}, Slow: {slowCount}, Blocked: {blockedCount}, Failed: {failedCount}. " +
            $"Average latency: {(averageLatency is null ? "n/a" : FormatLatency(averageLatency.Value))}.";
    }

    private static string FormatLatency(TimeSpan latency)
    {
        return $"{Math.Round(latency.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }
}
