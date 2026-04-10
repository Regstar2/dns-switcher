using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Core.Services;

public sealed class DnsBenchmarkService(
    DnsProfileService profileService,
    IDnsManager dnsManager,
    DnsTester dnsTester,
    IDnsProfileActivator profileActivator,
    IDnsBenchmarkHistoryStore historyStore,
    DnsBenchmarkSelector selector,
    ILogger<DnsBenchmarkService> logger)
{
    private const string RestoreProfileId = "__benchmark_restore__";
    private static readonly string[] FallbackDomains =
    [
        "cloudflare.com",
        "github.com",
        "openai.com",
    ];

    public async Task<DnsBenchmarkResult> BenchmarkProfilesAsync(
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var profiles = configuration.Profiles
            .Where(profile => profile.Mode == ProfileMode.Static)
            .ToArray();

        if (profiles.Length == 0)
        {
            throw new DnsOperationFailedException("No static DNS profiles are configured for benchmarking.");
        }

        var initialStatus = await dnsManager.GetStatusAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(initialStatus.AdapterName))
        {
            throw new NetworkAdapterNotFoundException("No suitable network adapter was selected.");
        }

        var restoreSnapshot = CaptureRestoreSnapshot(initialStatus);
        var profileResults = new List<DnsBenchmarkProfileResult>(profiles.Length);
        Exception? interruption = null;

        logger.LogInformation(
            "DNS benchmark started. Adapter: {AdapterSelection}. Profiles: {ProfileCount}.",
            adapterIdOrName ?? "<auto>",
            profiles.Length);

        try
        {
            foreach (var profile in profiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    logger.LogInformation("Benchmarking DNS profile {ProfileId}.", profile.Id);
                    await profileActivator
                        .ApplyTransientProfileAsync(profile, adapterIdOrName, cancellationToken)
                        .ConfigureAwait(false);

                    var testResult = await dnsTester.TestCurrentDnsAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
                    profileResults.Add(new DnsBenchmarkProfileResult(profile.Id, profile.Name, testResult, IsBest: false));
                }
                catch (Exception exception) when (!IsFatalBenchmarkException(exception))
                {
                    logger.LogWarning(exception, "Benchmark failed for DNS profile {ProfileId}.", profile.Id);
                    profileResults.Add(CreateFailedResult(configuration, initialStatus.AdapterName, profile, exception));
                }
            }
        }
        catch (Exception exception)
        {
            interruption = exception;
            logger.LogWarning(exception, "DNS benchmark was interrupted.");
        }

        var (restoreSucceeded, restoreDetails) = await RestoreAsync(restoreSnapshot, adapterIdOrName, cancellationToken).ConfigureAwait(false);

        if (profileResults.Count == 0 && interruption is not null)
        {
            throw interruption;
        }

        var bestProfile = selector.SelectBestProfile(profileResults);
        var finalizedResults = profileResults
            .Select(result => result with
            {
                IsBest = bestProfile is not null
                    && string.Equals(result.ProfileId, bestProfile.ProfileId, StringComparison.OrdinalIgnoreCase),
            })
            .ToArray();

        var benchmarkResult = new DnsBenchmarkResult(
            ExecutedAtUtc: DateTimeOffset.UtcNow,
            AdapterName: initialStatus.AdapterName,
            TotalProfiles: profiles.Length,
            ProfileResults: finalizedResults,
            BestProfileId: bestProfile?.ProfileId,
            BestProfileName: bestProfile?.ProfileName,
            OverallStatus: bestProfile?.TestResult.Status ?? DnsTestStatus.Failed,
            BestLatency: bestProfile?.TestResult.AverageLatency,
            RestoreSucceeded: restoreSucceeded,
            RestoreDetails: restoreDetails,
            WasInterrupted: interruption is not null,
            InterruptionReason: interruption?.Message,
            Details: BuildBenchmarkDetails(profiles.Length, finalizedResults, bestProfile, restoreSucceeded, restoreDetails, interruption));

        await historyStore.AppendAsync(benchmarkResult, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "DNS benchmark finished. Adapter: {AdapterName}. Tested: {TestedProfiles}/{TotalProfiles}. Best profile: {BestProfileId}. Restored: {RestoreSucceeded}. Interrupted: {Interrupted}",
            benchmarkResult.AdapterName ?? "<none>",
            benchmarkResult.ProfileResults.Count,
            benchmarkResult.TotalProfiles,
            benchmarkResult.BestProfileId ?? "<none>",
            benchmarkResult.RestoreSucceeded,
            benchmarkResult.WasInterrupted);

        return benchmarkResult;
    }

    private static DnsBenchmarkProfileResult CreateFailedResult(
        AppConfig configuration,
        string? adapterName,
        DnsProfile profile,
        Exception exception)
    {
        var domains = ResolveDomains(configuration, profile);
        var testResult = new DnsTestResult(
            AdapterName: adapterName,
            ProfileId: profile.Id,
            ProfileName: profile.Name,
            DnsServers: profile.Ipv4.Concat(profile.Ipv6).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Domains: domains,
            DomainResults: [],
            Status: DnsTestStatus.Failed,
            AverageLatency: null,
            Details: exception.Message);

        return new DnsBenchmarkProfileResult(profile.Id, profile.Name, testResult, IsBest: false);
    }

    private static bool IsFatalBenchmarkException(Exception exception)
    {
        return exception is DnsAgentUnavailableException
            or DnsOperationRequiresAdminException
            or NetworkAdapterNotFoundException
            or NetworkAdapterDisabledException
            or OperationCanceledException;
    }

    private async Task<(bool Succeeded, string Details)> RestoreAsync(
        RestoreSnapshot snapshot,
        string? adapterIdOrName,
        CancellationToken cancellationToken)
    {
        try
        {
            if (snapshot.UseDhcp)
            {
                await profileActivator.ResetToDhcpTransientAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
                return (true, "Original DHCP DNS mode was restored.");
            }

            if (snapshot.RestoreProfile is not null)
            {
                await profileActivator.ApplyTransientProfileAsync(snapshot.RestoreProfile, adapterIdOrName, cancellationToken).ConfigureAwait(false);
                return (true, "Original DNS server settings were restored.");
            }

            return (true, "No DNS restore operation was required.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to restore original DNS state after benchmark.");
            return (false, $"Failed to restore original DNS state: {exception.Message}");
        }
    }

    private static RestoreSnapshot CaptureRestoreSnapshot(DnsStatus status)
    {
        if (status.Mode == DnsMode.Dhcp)
        {
            return new RestoreSnapshot(UseDhcp: true, RestoreProfile: null);
        }

        var hasManualIpv4 = status.Ipv4.Mode == DnsMode.Manual || status.Ipv4.NameServers.Count > 0;
        var hasManualIpv6 = status.Ipv6.Mode == DnsMode.Manual || status.Ipv6.NameServers.Count > 0;

        if (!hasManualIpv4 && !hasManualIpv6)
        {
            return new RestoreSnapshot(UseDhcp: true, RestoreProfile: null);
        }

        var restoreProfile = new DnsProfile
        {
            Id = RestoreProfileId,
            Name = "Benchmark Restore Snapshot",
            Description = "Temporary DNS restore snapshot created by benchmark.",
            Mode = ProfileMode.Static,
            Ipv4 = hasManualIpv4 ? [.. status.Ipv4.NameServers] : [],
            Ipv6 = hasManualIpv6 ? [.. status.Ipv6.NameServers] : [],
        };

        return new RestoreSnapshot(UseDhcp: false, RestoreProfile: restoreProfile);
    }

    private static IReadOnlyList<string> ResolveDomains(AppConfig configuration, DnsProfile selectedProfile)
    {
        var fromSelectedProfile = selectedProfile.TestDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(NormalizeDomain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (fromSelectedProfile.Length > 0)
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

    private static string BuildBenchmarkDetails(
        int totalProfiles,
        IReadOnlyList<DnsBenchmarkProfileResult> finalizedResults,
        DnsBenchmarkProfileResult? bestProfile,
        bool restoreSucceeded,
        string restoreDetails,
        Exception? interruption)
    {
        var okCount = finalizedResults.Count(result => result.TestResult.Status == DnsTestStatus.Ok);
        var slowCount = finalizedResults.Count(result => result.TestResult.Status == DnsTestStatus.Slow);
        var failedCount = finalizedResults.Count(result => result.TestResult.Status == DnsTestStatus.Failed);

        var details =
            $"Tested {finalizedResults.Count}/{totalProfiles} profiles. " +
            $"OK: {okCount}, Slow: {slowCount}, Failed: {failedCount}. " +
            $"Best profile: {(bestProfile is null ? "<none>" : $"{bestProfile.ProfileName} ({bestProfile.ProfileId})")} " +
            $"with latency {(bestProfile?.TestResult.AverageLatency is null ? "n/a" : FormatLatency(bestProfile.TestResult.AverageLatency.Value))}.";

        if (interruption is not null)
        {
            details += $" Benchmark was interrupted: {interruption.Message}.";
        }

        details += $" {restoreDetails}";
        details += restoreSucceeded ? string.Empty : " DNS state may require manual recovery.";
        return details.Trim();
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimEnd('.');
    }

    private static string FormatLatency(TimeSpan latency)
    {
        return $"{Math.Round(latency.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }

    private sealed record RestoreSnapshot(bool UseDhcp, DnsProfile? RestoreProfile);
}
