using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Core.Services;

public sealed class DnsHealthFailoverService(
    DnsProfileService profileService,
    IDnsManager dnsManager,
    DnsTester dnsTester,
    IDnsProfileActivator profileActivator,
    IDnsHealthSettingsStore settingsStore,
    IDnsHealthStateStore stateStore,
    ILogger<DnsHealthFailoverService> logger,
    Func<DateTimeOffset>? utcNowProvider = null)
{
    public Task<DnsHealthSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return settingsStore.LoadAsync(cancellationToken);
    }

    public Task SaveSettingsAsync(DnsHealthSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settingsStore.SaveAsync(NormalizeSettings(settings), cancellationToken);
    }

    public Task<DnsHealthState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return stateStore.LoadAsync(cancellationToken);
    }

    public async Task<DnsHealthEvaluationResult> EvaluateAsync(
        string? adapterIdOrName = null,
        CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        var settings = NormalizeSettings(await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false));
        var previousState = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (!settings.Enabled)
        {
            var disabledState = DnsHealthState.Disabled(now);
            await stateStore.SaveAsync(disabledState, cancellationToken).ConfigureAwait(false);
            return new DnsHealthEvaluationResult(
                DnsHealthStatus.Disabled,
                SwitchedProfile: false,
                ActiveProfileId: null,
                TargetProfileId: null,
                Details: disabledState.LastAction ?? "Health monitoring is disabled.",
                State: disabledState,
                TestResult: null);
        }

        var configuration = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var dnsStatus = await dnsManager.GetStatusAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
        var activeProfileId = dnsStatus.MatchedProfileId ?? configuration.ActiveProfileId;
        var testDomains = ResolveTestDomains(settings);
        var testResult = await dnsTester
            .TestCurrentDnsAsync(adapterIdOrName, testDomains, cancellationToken)
            .ConfigureAwait(false);
        var expectedAddressFailure = GetExpectedAddressFailure(settings, testResult);
        var isFailed = testResult.Status == DnsTestStatus.Failed || expectedAddressFailure is not null;
        var cooldownActive = previousState.CooldownUntilUtc is not null && previousState.CooldownUntilUtc > now;

        if (!isFailed)
        {
            var successfulState = BuildSuccessfulState(previousState, settings, now, activeProfileId, testResult);
            await stateStore.SaveAsync(successfulState, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "DNS health check completed. Status: {Status}. Active profile: {ActiveProfileId}. Details: {Details}",
                successfulState.Status,
                activeProfileId ?? "<none>",
                testResult.Details);

            return new DnsHealthEvaluationResult(
                successfulState.Status,
                SwitchedProfile: false,
                ActiveProfileId: activeProfileId,
                TargetProfileId: null,
                Details: successfulState.LastAction ?? testResult.Details,
                State: successfulState,
                TestResult: testResult);
        }

        var failureReason = expectedAddressFailure ?? testResult.Details;
        var failureState = BuildFailureState(previousState, settings, now, activeProfileId, failureReason, cooldownActive);

        if (failureState.ConsecutiveFailures < settings.FailureThreshold)
        {
            await stateStore.SaveAsync(failureState, cancellationToken).ConfigureAwait(false);
            return new DnsHealthEvaluationResult(
                failureState.Status,
                SwitchedProfile: false,
                ActiveProfileId: activeProfileId,
                TargetProfileId: null,
                Details: failureState.LastAction ?? failureReason,
                State: failureState,
                TestResult: testResult);
        }

        var (finalState, switched, targetProfileId) = await HandleThresholdFailureAsync(
            configuration,
            settings,
            failureState,
            activeProfileId,
            adapterIdOrName,
            cooldownActive,
            cancellationToken).ConfigureAwait(false);

        await stateStore.SaveAsync(finalState, cancellationToken).ConfigureAwait(false);
        return new DnsHealthEvaluationResult(
            finalState.Status,
            switched,
            activeProfileId,
            targetProfileId,
            finalState.LastAction ?? failureReason,
            finalState,
            testResult);
    }

    private async Task<(DnsHealthState State, bool Switched, string? TargetProfileId)> HandleThresholdFailureAsync(
        AppConfig configuration,
        DnsHealthSettings settings,
        DnsHealthState failureState,
        string? activeProfileId,
        string? adapterIdOrName,
        bool cooldownActive,
        CancellationToken cancellationToken)
    {
        if (settings.ActionOnFailure == DnsHealthFailureAction.NotifyOnly)
        {
            logger.LogWarning(
                "DNS health failure threshold was reached for profile {ActiveProfileId}. Action: notify only. Reason: {Reason}",
                activeProfileId ?? "<none>",
                failureState.LastFailureReason ?? "<none>");
            return (failureState with { LastAction = "Failure threshold reached. Notify-only mode is active." }, false, null);
        }

        if (cooldownActive)
        {
            return (failureState with
            {
                Status = DnsHealthStatus.Cooldown,
                LastAction = $"Failure threshold reached, but failover is in cooldown until {failureState.CooldownUntilUtc:O}.",
            }, false, null);
        }

        var targetProfile = SelectFailoverProfile(configuration, settings, activeProfileId);

        if (targetProfile is null)
        {
            return (failureState with
            {
                LastAction = "Failure threshold reached, but no usable failover profile was found.",
            }, false, null);
        }

        await profileActivator.ApplyTransientProfileAsync(targetProfile, adapterIdOrName, cancellationToken)
            .ConfigureAwait(false);
        await profileService.SetActiveProfileAsync(targetProfile.Id, cancellationToken).ConfigureAwait(false);

        var now = GetUtcNow();
        var state = failureState with
        {
            LastFailoverUtc = now,
            CooldownUntilUtc = now.AddSeconds(settings.CooldownSeconds),
            LastFailoverProfileId = targetProfile.Id,
            LastAction = $"Switched to failover profile '{targetProfile.Id}' because DNS health failed: {failureState.LastFailureReason}",
        };

        logger.LogWarning(
            "DNS health failover switched profile from {ActiveProfileId} to {TargetProfileId}. Reason: {Reason}. Cooldown until: {CooldownUntilUtc}",
            activeProfileId ?? "<none>",
            targetProfile.Id,
            failureState.LastFailureReason ?? "<none>",
            state.CooldownUntilUtc);

        return (state, true, targetProfile.Id);
    }

    private static DnsProfile? SelectFailoverProfile(
        AppConfig configuration,
        DnsHealthSettings settings,
        string? activeProfileId)
    {
        return settings.ActionOnFailure switch
        {
            DnsHealthFailureAction.SwitchToFallbackProfile => SelectFallbackProfile(configuration, settings.FallbackProfileId, activeProfileId),
            DnsHealthFailureAction.SwitchToNextProfile => SelectNextProfile(configuration, settings.FailoverChain, activeProfileId),
            _ => null,
        };
    }

    private static DnsProfile? SelectFallbackProfile(AppConfig configuration, string? fallbackProfileId, string? activeProfileId)
    {
        if (string.IsNullOrWhiteSpace(fallbackProfileId)
            || string.Equals(fallbackProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return configuration.Profiles.FirstOrDefault(profile =>
            profile.Mode == ProfileMode.Static
            && string.Equals(profile.Id, fallbackProfileId, StringComparison.OrdinalIgnoreCase));
    }

    private static DnsProfile? SelectNextProfile(
        AppConfig configuration,
        IReadOnlyList<string> configuredChain,
        string? activeProfileId)
    {
        var staticProfiles = configuration.Profiles
            .Where(profile => profile.Mode == ProfileMode.Static)
            .ToArray();
        var chain = configuredChain.Count > 0
            ? configuredChain
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => staticProfiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(profile => profile is not null)
                .Cast<DnsProfile>()
                .DistinctBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : staticProfiles;

        if (chain.Length <= 1)
        {
            return chain.FirstOrDefault(profile =>
                !string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase));
        }

        var currentIndex = Array.FindIndex(chain, profile =>
            string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase));

        for (var offset = 1; offset <= chain.Length; offset++)
        {
            var candidate = chain[(Math.Max(currentIndex, 0) + offset) % chain.Length];

            if (!string.Equals(candidate.Id, activeProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static DnsHealthState BuildSuccessfulState(
        DnsHealthState previousState,
        DnsHealthSettings settings,
        DateTimeOffset now,
        string? activeProfileId,
        DnsTestResult testResult)
    {
        var successes = previousState.ConsecutiveSuccesses + 1;
        var recovered = successes >= settings.RecoveryThreshold;
        var status = recovered || previousState.Status is DnsHealthStatus.Healthy or DnsHealthStatus.Disabled
            ? testResult.Status == DnsTestStatus.Slow ? DnsHealthStatus.Degraded : DnsHealthStatus.Healthy
            : DnsHealthStatus.Degraded;

        return previousState with
        {
            Status = status,
            EnabledSnapshot = true,
            ConsecutiveFailures = 0,
            ConsecutiveSuccesses = successes,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = now,
            ActiveProfileId = activeProfileId,
            LastFailureReason = recovered ? null : previousState.LastFailureReason,
            LastAction = recovered
                ? $"DNS health check passed. {testResult.Details}"
                : $"DNS health check passed, waiting for recovery threshold {successes}/{settings.RecoveryThreshold}.",
        };
    }

    private static DnsHealthState BuildFailureState(
        DnsHealthState previousState,
        DnsHealthSettings settings,
        DateTimeOffset now,
        string? activeProfileId,
        string failureReason,
        bool cooldownActive)
    {
        var failures = previousState.ConsecutiveFailures + 1;
        var thresholdReached = failures >= settings.FailureThreshold;

        return previousState with
        {
            Status = cooldownActive ? DnsHealthStatus.Cooldown : thresholdReached ? DnsHealthStatus.Failed : DnsHealthStatus.Degraded,
            EnabledSnapshot = true,
            ConsecutiveFailures = failures,
            ConsecutiveSuccesses = 0,
            LastCheckedUtc = now,
            LastFailureUtc = now,
            ActiveProfileId = activeProfileId,
            LastFailureReason = failureReason,
            LastAction = thresholdReached
                ? $"DNS health failure threshold reached {failures}/{settings.FailureThreshold}. {failureReason}"
                : $"DNS health failure {failures}/{settings.FailureThreshold}. {failureReason}",
        };
    }

    private static string? GetExpectedAddressFailure(DnsHealthSettings settings, DnsTestResult result)
    {
        if (settings.CheckMode != DnsHealthCheckMode.ResolveWithExpectedIp || settings.ExpectedAddresses.Count == 0)
        {
            return null;
        }

        foreach (var domainResult in result.DomainResults)
        {
            if (!TryGetExpectedAddresses(settings, domainResult.Domain, out var expectedAddresses))
            {
                continue;
            }

            var actualAddresses = domainResult.AnswerAddresses ?? [];

            if (actualAddresses.Count == 0)
            {
                return $"Domain '{domainResult.Domain}' resolved without exposed answer addresses.";
            }

            if (!actualAddresses.Any(actual =>
                    expectedAddresses.Any(expected => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))))
            {
                return
                    $"Domain '{domainResult.Domain}' resolved to [{string.Join(", ", actualAddresses)}], " +
                    $"expected one of [{string.Join(", ", expectedAddresses)}].";
            }
        }

        return null;
    }

    private static bool TryGetExpectedAddresses(
        DnsHealthSettings settings,
        string domain,
        out IReadOnlyList<string> expectedAddresses)
    {
        foreach (var pair in settings.ExpectedAddresses)
        {
            if (string.Equals(pair.Key, domain, StringComparison.OrdinalIgnoreCase))
            {
                expectedAddresses = pair.Value
                    .Where(address => !string.IsNullOrWhiteSpace(address))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return expectedAddresses.Count > 0;
            }
        }

        expectedAddresses = [];
        return false;
    }

    private static IReadOnlyList<string> ResolveTestDomains(DnsHealthSettings settings)
    {
        var domains = settings.TestDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim().TrimEnd('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return domains.Length > 0 ? domains : DnsHealthSettings.Default.TestDomains;
    }

    private static DnsHealthSettings NormalizeSettings(DnsHealthSettings settings)
    {
        return settings with
        {
            MonitorIntervalSeconds = Math.Clamp(settings.MonitorIntervalSeconds, 15, 86_400),
            FailureThreshold = Math.Max(1, settings.FailureThreshold),
            RecoveryThreshold = Math.Max(1, settings.RecoveryThreshold),
            CooldownSeconds = Math.Max(0, settings.CooldownSeconds),
            FailoverChain = settings.FailoverChain
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TestDomains = ResolveTestDomains(settings).ToList(),
            ExpectedAddresses = settings.ExpectedAddresses
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value.Count > 0)
                .ToDictionary(
                    pair => pair.Key.Trim().TrimEnd('.'),
                    pair => pair.Value
                        .Where(address => !string.IsNullOrWhiteSpace(address))
                        .Select(address => address.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase),
        };
    }

    private DateTimeOffset GetUtcNow()
    {
        return utcNowProvider?.Invoke() ?? DateTimeOffset.UtcNow;
    }
}
