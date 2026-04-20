using System.Runtime.Versioning;
using DnsSwitcher.Cli;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Agent;
using DnsSwitcher.Infrastructure.Windows.Dns;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;

[assembly: SupportedOSPlatform("windows")]

try
{
    var exitCode = await RunAsync(args).ConfigureAwait(false);
    return exitCode;
}
catch (Exception exception)
{
    return HandleException(exception, interactive: false);
}

static async Task<int> RunAsync(string[] args)
{
    TryConfigureConsoleEncoding();

    var parseResult = CliArgumentParser.Parse(args);

    if (!parseResult.IsSuccess)
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
        Console.Error.WriteLine();
        PrintHelp();
        return CliExitCodes.InvalidArguments;
    }

    var invocation = parseResult.Invocation!;

    if (invocation.Command == CliCommand.Help)
    {
        PrintHelp();
        return CliExitCodes.Success;
    }

    if (invocation.IsInteractive)
    {
        try
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                Console.Error.WriteLine("Interactive mode requires an attached console window.");
                Console.Error.WriteLine("Run without redirected input/output, or use a command like 'status' or 'profiles'.");
                return CliExitCodes.InvalidArguments;
            }
        }
        catch (IOException)
        {
            Console.Error.WriteLine("Interactive mode requires an attached console window.");
            return CliExitCodes.InvalidArguments;
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine("Interactive mode requires an attached console window.");
            return CliExitCodes.InvalidArguments;
        }

        return await RunInteractiveAsync(invocation).ConfigureAwait(false);
    }

    using var host = CreateHost(invocation);
    var logger = host.LoggerFactory.CreateLogger("DnsSwitcher.Cli");

    try
    {
        logger.LogInformation(
            "DnsSwitcher CLI starting. Command: {Command}. Interactive: {Interactive}. Adapter: {AdapterSelection}. Config: {ConfigPath}",
            invocation.Command?.ToString() ?? "<interactive>",
            invocation.IsInteractive,
            invocation.AdapterSelection ?? "<auto>",
            invocation.ConfigPath ?? "<default>");

        await host.ProfileService.EnsureInitializedAsync().ConfigureAwait(false);
        var exitCode = await ExecuteCommandWithHandlingAsync(host, invocation, interactive: false).ConfigureAwait(false);
        logger.LogInformation("DnsSwitcher CLI finished with exit code {ExitCode}.", exitCode);
        return exitCode;
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "DnsSwitcher CLI failed.");
        throw;
    }
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        DnsSwitcher Console

        Run without arguments to open the interactive console menu.

        Commands:
          dns-switcher profiles
          dns-switcher adapters
          dns-switcher status
          dns-switcher current
          dns-switcher apply <profile-id>
          dns-switcher reset
          dns-switcher test
          dns-switcher test-sites
          dns-switcher benchmark
          dns-switcher health <status|enable|disable|check|chain|fallback|action|domains>
          dns-switcher split-dns <status|enable|disable|list|add|remove|update|enable-rule|disable-rule|test|apply|reset>
          dns-switcher validate-config
          dns-switcher service <install|reinstall|uninstall|start|stop|status> [agent-exe-path]

        Options:
          --adapter <id|name>   Use a specific adapter instead of auto-selection
          --config <path>       Use a custom profiles.json path or config directory
          -h, --help            Show this help

        Legacy aliases:
          list -> profiles
          current -> status
          switch, enable -> apply
          disable -> reset
          validate -> validate-config

        Exit codes:
          0 success
          1 invalid arguments
          2 invalid config
          3 profile not found
          4 adapter error
          5 administrator rights required
          6 DNS operation failed
          7 unexpected error
        """);
}

static async Task PrintProfilesAsync(WindowsDnsSwitcherHost host)
{
    var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(false);

    if (configuration.Profiles.Count == 0)
    {
        Console.WriteLine("No DNS profiles configured.");
        return;
    }

    foreach (var profile in configuration.Profiles)
    {
        var activeMarker = string.Equals(profile.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase)
            ? "*"
            : " ";

        Console.WriteLine($"{activeMarker} {profile.Id} - {profile.Name}");
        Console.WriteLine($"    Mode: {profile.Mode}");
        Console.WriteLine($"    IPv4: {(profile.Ipv4.Count == 0 ? "<none>" : string.Join(", ", profile.Ipv4))}");

        if (profile.Ipv6.Count > 0)
        {
            Console.WriteLine($"    IPv6: {string.Join(", ", profile.Ipv6)}");
        }

        if (profile.Tags.Count > 0)
        {
            Console.WriteLine($"    Tags: {string.Join(", ", profile.Tags)}");
        }
    }
}

static async Task PrintStatusAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(false);
    var activeProfile = await host.ProfileService.GetActiveProfileAsync().ConfigureAwait(false);
    var dnsStatus = await host.DnsManager.GetStatusAsync(adapterSelection).ConfigureAwait(false);
    var agentAvailable = await host.AgentDnsSwitchService.IsAgentAvailableAsync().ConfigureAwait(false);
    var agentServiceInfo = await host.AgentServiceManager.GetInfoAsync().ConfigureAwait(false);
    var healthSettings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);
    var healthState = await host.DnsHealthFailoverService.GetStateAsync().ConfigureAwait(false);
    var splitDnsConfiguration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(false);

    Console.WriteLine($"Portable data: {host.Paths.AppDirectory}");
    Console.WriteLine($"Profiles file: {host.Paths.ProfilesFilePath}");
    Console.WriteLine($"Adapter override: {adapterSelection ?? "<auto>"}");
    Console.WriteLine($"Agent service status: {agentServiceInfo.Status}");
    Console.WriteLine($"Agent service path: {agentServiceInfo.ServiceBinaryPath ?? "<not installed>"}");
    Console.WriteLine($"Expected agent path: {agentServiceInfo.ExpectedBinaryPath}");
    Console.WriteLine($"Agent service path current: {agentServiceInfo.PointsToExpectedPath}");
    Console.WriteLine($"Agent available: {agentAvailable}");
    Console.WriteLine($"Health monitoring: {(healthSettings.Enabled ? "Enabled" : "Disabled")} ({healthState.Status})");
    Console.WriteLine($"Health last action: {healthState.LastAction ?? "<none>"}");
    Console.WriteLine($"Split DNS: {(splitDnsConfiguration.Enabled ? "Enabled" : "Disabled")} ({splitDnsConfiguration.Rules.Count} rule(s))");
    Console.WriteLine($"Config active profile id: {configuration.ActiveProfileId ?? "<none>"}");
    Console.WriteLine($"Config active profile: {activeProfile?.Name ?? "<none>"}");
    Console.WriteLine($"Selected adapter: {dnsStatus.AdapterName ?? "<none>"}");
    Console.WriteLine($"Current DNS mode: {dnsStatus.Mode}");
    Console.WriteLine($"Matched profile id: {dnsStatus.MatchedProfileId ?? "<none>"}");
    Console.WriteLine($"IPv4 mode: {dnsStatus.Ipv4.Mode}");
    Console.WriteLine($"IPv4 DNS: {(dnsStatus.Ipv4.NameServers.Count == 0 ? "<none>" : string.Join(", ", dnsStatus.Ipv4.NameServers))}");
    Console.WriteLine($"IPv6 mode: {dnsStatus.Ipv6.Mode}");
    Console.WriteLine($"IPv6 DNS: {(dnsStatus.Ipv6.NameServers.Count == 0 ? "<none>" : string.Join(", ", dnsStatus.Ipv6.NameServers))}");
    Console.WriteLine($"System managed by app: {dnsStatus.IsManaged}");
    Console.WriteLine($"System DNS details: {dnsStatus.Details}");
}

static async Task ValidateConfigAsync(WindowsDnsSwitcherHost host)
{
    _ = await host.ProfileStore.LoadAsync().ConfigureAwait(false);
    Console.WriteLine($"profiles.json is valid: {host.Paths.ProfilesFilePath}");
}

static async Task PrintDnsTestAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var result = await host.DnsTester.TestCurrentDnsAsync(adapterSelection).ConfigureAwait(false);

    Console.WriteLine($"Adapter target: {adapterSelection ?? "<auto>"}");
    Console.WriteLine($"Selected adapter: {result.AdapterName ?? "<none>"}");
    Console.WriteLine($"Test profile: {DiagnosticTextFormatter.FormatProfileLabel(result.ProfileName, result.ProfileId)}");
    Console.WriteLine($"DNS servers: {(result.DnsServers.Count == 0 ? "<none>" : string.Join(", ", result.DnsServers))}");
    Console.WriteLine($"Domains: {(result.Domains.Count == 0 ? "<none>" : string.Join(", ", result.Domains))}");
    Console.WriteLine($"Overall status: {result.Status}");
    Console.WriteLine($"Average latency: {DiagnosticTextFormatter.FormatLatency(result.AverageLatency)}");
    Console.WriteLine($"Details: {result.Details}");

    if (result.DomainResults.Count == 0)
    {
        return;
    }

    Console.WriteLine("Per-domain:");

    foreach (var domainResult in result.DomainResults)
    {
        Console.WriteLine(
            $"  - {domainResult.Domain}: {domainResult.Status} | " +
            $"success {domainResult.SuccessfulAttempts}/{domainResult.TotalAttempts} | " +
            $"avg {DiagnosticTextFormatter.FormatLatency(domainResult.AverageLatency)} | " +
            $"best {DiagnosticTextFormatter.FormatLatency(domainResult.BestLatency)}");
        Console.WriteLine($"    {domainResult.Details}");
    }
}

static async Task PrintSiteConnectivityTestAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var result = await host.ConnectivityTester.TestCurrentSitesAsync(adapterSelection).ConfigureAwait(false);

    Console.WriteLine($"Adapter target: {adapterSelection ?? "<auto>"}");
    Console.WriteLine($"Selected adapter: {result.AdapterName ?? "<none>"}");
    Console.WriteLine($"Test profile: {DiagnosticTextFormatter.FormatProfileLabel(result.ProfileName, result.ProfileId)}");
    Console.WriteLine($"URLs: {(result.Urls.Count == 0 ? "<none>" : string.Join(", ", result.Urls))}");
    Console.WriteLine($"Overall status: {result.Status}");
    Console.WriteLine($"Average latency: {DiagnosticTextFormatter.FormatLatency(result.AverageLatency)}");
    Console.WriteLine($"Details: {result.Details}");

    if (result.UrlResults.Count == 0)
    {
        return;
    }

    Console.WriteLine("Per-url:");

    foreach (var urlResult in result.UrlResults)
    {
        Console.WriteLine(
            $"  - {urlResult.Url}: {urlResult.Status} | " +
            $"success {urlResult.SuccessfulAttempts}/{urlResult.TotalAttempts} | " +
            $"avg {DiagnosticTextFormatter.FormatLatency(urlResult.AverageLatency)} | " +
            $"http {(urlResult.HttpStatusCode?.ToString() ?? "<none>")} via {urlResult.HttpMethod}");
        Console.WriteLine($"    DNS: {urlResult.Dns.Details}");
        Console.WriteLine($"    TCP: {urlResult.Connect.Details}");

        if (!string.Equals(urlResult.Tls.Details, "TLS not required.", StringComparison.Ordinal))
        {
            Console.WriteLine($"    TLS: {urlResult.Tls.Details}");
        }

        Console.WriteLine($"    HTTP: {urlResult.Http.Details}");
        Console.WriteLine($"    {urlResult.Details}");
    }
}

static async Task PrintBenchmarkAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var result = await host.DnsBenchmarkService.BenchmarkProfilesAsync(adapterSelection).ConfigureAwait(false);

    Console.WriteLine($"Adapter target: {adapterSelection ?? "<auto>"}");
    Console.WriteLine($"Selected adapter: {result.AdapterName ?? "<none>"}");
    Console.WriteLine($"Tested profiles: {result.ProfileResults.Count}/{result.TotalProfiles}");
    Console.WriteLine($"Best profile: {DiagnosticTextFormatter.FormatProfileLabel(result.BestProfileName, result.BestProfileId)}");
    Console.WriteLine($"Best latency: {DiagnosticTextFormatter.FormatLatency(result.BestLatency)}");
    Console.WriteLine($"Overall status: {result.OverallStatus}");
    Console.WriteLine($"Restore: {(result.RestoreSucceeded ? "OK" : "Failed")} - {result.RestoreDetails}");

    if (result.WasInterrupted)
    {
        Console.WriteLine($"Interrupted: {result.InterruptionReason ?? "<none>"}");
    }

    Console.WriteLine($"Details: {result.Details}");

    if (result.ProfileResults.Count == 0)
    {
        return;
    }

    Console.WriteLine("Per-profile:");

    foreach (var profileResult in result.ProfileResults)
    {
        Console.WriteLine(
            $"  - {profileResult.ProfileName} ({profileResult.ProfileId}): {profileResult.TestResult.Status} | " +
            $"avg {DiagnosticTextFormatter.FormatLatency(profileResult.TestResult.AverageLatency)}" +
            $"{(profileResult.IsBest ? " | BEST" : string.Empty)}");
        Console.WriteLine($"    {profileResult.TestResult.Details}");
    }
}

static async Task<int> ExecuteHealthCommandAsync(WindowsDnsSwitcherHost host, CliInvocation invocation)
{
    var args = invocation.Arguments;
    var command = args.Count > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

    switch (command)
    {
        case "status":
            await PrintHealthStatusAsync(host).ConfigureAwait(false);
            return CliExitCodes.Success;
        case "enable":
            await SetHealthEnabledAsync(host, enabled: true).ConfigureAwait(false);
            Console.WriteLine("DNS health monitoring enabled.");
            return CliExitCodes.Success;
        case "disable":
            await SetHealthEnabledAsync(host, enabled: false).ConfigureAwait(false);
            Console.WriteLine("DNS health monitoring disabled.");
            return CliExitCodes.Success;
        case "check":
        case "run":
            await PrintHealthCheckAsync(host, invocation.AdapterSelection).ConfigureAwait(false);
            return CliExitCodes.Success;
        case "chain":
            return await ExecuteHealthChainCommandAsync(host, args).ConfigureAwait(false);
        case "fallback":
            return await ExecuteHealthFallbackCommandAsync(host, args).ConfigureAwait(false);
        case "action":
            return await ExecuteHealthActionCommandAsync(host, args).ConfigureAwait(false);
        case "domains":
            return await ExecuteHealthDomainsCommandAsync(host, args).ConfigureAwait(false);
        default:
            Console.Error.WriteLine("Usage: dns-switcher health <status|enable|disable|check|chain|fallback|action|domains> [args]");
            return CliExitCodes.InvalidArguments;
    }
}

static async Task PrintHealthStatusAsync(WindowsDnsSwitcherHost host)
{
    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);
    var state = await host.DnsHealthFailoverService.GetStateAsync().ConfigureAwait(false);

    Console.WriteLine($"Enabled: {settings.Enabled}");
    Console.WriteLine($"Monitor interval: {settings.MonitorIntervalSeconds} sec");
    Console.WriteLine($"Failure threshold: {settings.FailureThreshold}");
    Console.WriteLine($"Recovery threshold: {settings.RecoveryThreshold}");
    Console.WriteLine($"Cooldown: {settings.CooldownSeconds} sec");
    Console.WriteLine($"Check mode: {settings.CheckMode}");
    Console.WriteLine($"Action on failure: {settings.ActionOnFailure}");
    Console.WriteLine($"Fallback profile: {settings.FallbackProfileId ?? "<none>"}");
    Console.WriteLine($"Failover chain: {(settings.FailoverChain.Count == 0 ? "<auto static profiles>" : string.Join(", ", settings.FailoverChain))}");
    Console.WriteLine($"Test domains: {string.Join(", ", settings.TestDomains)}");
    Console.WriteLine($"State: {state.Status}");
    Console.WriteLine($"Consecutive failures: {state.ConsecutiveFailures}");
    Console.WriteLine($"Consecutive successes: {state.ConsecutiveSuccesses}");
    Console.WriteLine($"Last checked UTC: {state.LastCheckedUtc?.ToString("O") ?? "<never>"}");
    Console.WriteLine($"Last successful check UTC: {state.LastSuccessfulCheckUtc?.ToString("O") ?? "<never>"}");
    Console.WriteLine($"Cooldown until UTC: {state.CooldownUntilUtc?.ToString("O") ?? "<none>"}");
    Console.WriteLine($"Last failure reason: {state.LastFailureReason ?? "<none>"}");
    Console.WriteLine($"Last action: {state.LastAction ?? "<none>"}");
}

static async Task SetHealthEnabledAsync(WindowsDnsSwitcherHost host, bool enabled)
{
    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);
    await host.DnsHealthFailoverService.SaveSettingsAsync(settings with { Enabled = enabled }).ConfigureAwait(false);
}

static async Task PrintHealthCheckAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var result = await host.DnsHealthFailoverService.EvaluateAsync(adapterSelection).ConfigureAwait(false);

    Console.WriteLine($"Status: {result.Status}");
    Console.WriteLine($"Active profile: {result.ActiveProfileId ?? "<none>"}");
    Console.WriteLine($"Target profile: {result.TargetProfileId ?? "<none>"}");
    Console.WriteLine($"Switched profile: {result.SwitchedProfile}");
    Console.WriteLine($"Details: {result.Details}");

    if (result.TestResult is not null)
    {
        Console.WriteLine($"DNS test: {result.TestResult.Status}");
        Console.WriteLine($"Average latency: {DiagnosticTextFormatter.FormatLatency(result.TestResult.AverageLatency)}");
        Console.WriteLine($"DNS test details: {result.TestResult.Details}");
    }
}

static async Task<int> ExecuteHealthChainCommandAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);

    if (args.Count == 1 || string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(settings.FailoverChain.Count == 0
            ? "Failover chain: <auto static profiles>"
            : $"Failover chain: {string.Join(", ", settings.FailoverChain)}");
        return CliExitCodes.Success;
    }

    if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
    {
        await host.DnsHealthFailoverService.SaveSettingsAsync(settings with { FailoverChain = [] }).ConfigureAwait(false);
        Console.WriteLine("Failover chain cleared. Static profiles will be used automatically.");
        return CliExitCodes.Success;
    }

    if (string.Equals(args[1], "set", StringComparison.OrdinalIgnoreCase) && args.Count > 2)
    {
        await host.DnsHealthFailoverService
            .SaveSettingsAsync(settings with { FailoverChain = args.Skip(2).ToList() })
            .ConfigureAwait(false);
        Console.WriteLine($"Failover chain set: {string.Join(", ", args.Skip(2))}");
        return CliExitCodes.Success;
    }

    Console.Error.WriteLine("Usage: dns-switcher health chain [list|clear|set <profile-id>...]");
    return CliExitCodes.InvalidArguments;
}

static async Task<int> ExecuteHealthFallbackCommandAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count != 2)
    {
        Console.Error.WriteLine("Usage: dns-switcher health fallback <profile-id|none>");
        return CliExitCodes.InvalidArguments;
    }

    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);
    var fallbackProfileId = string.Equals(args[1], "none", StringComparison.OrdinalIgnoreCase) ? null : args[1];
    await host.DnsHealthFailoverService.SaveSettingsAsync(settings with { FallbackProfileId = fallbackProfileId }).ConfigureAwait(false);
    Console.WriteLine($"Fallback profile: {fallbackProfileId ?? "<none>"}");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteHealthActionCommandAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count != 2 || !TryParseHealthAction(args[1], out var action))
    {
        Console.Error.WriteLine("Usage: dns-switcher health action <notify-only|next|fallback>");
        return CliExitCodes.InvalidArguments;
    }

    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);
    await host.DnsHealthFailoverService.SaveSettingsAsync(settings with { ActionOnFailure = action }).ConfigureAwait(false);
    Console.WriteLine($"Action on failure: {action}");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteHealthDomainsCommandAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    var settings = await host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(false);

    if (args.Count == 1 || string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Test domains: {string.Join(", ", settings.TestDomains)}");
        return CliExitCodes.Success;
    }

    if (string.Equals(args[1], "set", StringComparison.OrdinalIgnoreCase) && args.Count > 2)
    {
        await host.DnsHealthFailoverService
            .SaveSettingsAsync(settings with { TestDomains = args.Skip(2).ToList() })
            .ConfigureAwait(false);
        Console.WriteLine($"Test domains set: {string.Join(", ", args.Skip(2))}");
        return CliExitCodes.Success;
    }

    Console.Error.WriteLine("Usage: dns-switcher health domains [list|set <domain>...]");
    return CliExitCodes.InvalidArguments;
}

static bool TryParseHealthAction(string value, out DnsHealthFailureAction action)
{
    switch (value.Trim().ToLowerInvariant())
    {
        case "notify":
        case "notify-only":
            action = DnsHealthFailureAction.NotifyOnly;
            return true;
        case "next":
        case "next-profile":
            action = DnsHealthFailureAction.SwitchToNextProfile;
            return true;
        case "fallback":
        case "fallback-profile":
            action = DnsHealthFailureAction.SwitchToFallbackProfile;
            return true;
        default:
            action = default;
            return false;
    }
}

static async Task<int> ExecuteSplitDnsCommandAsync(WindowsDnsSwitcherHost host, CliInvocation invocation)
{
    var args = invocation.Arguments;
    var command = args.Count > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

    switch (command)
    {
        case "status":
        case "list":
            await PrintSplitDnsConfigurationAsync(host).ConfigureAwait(false);
            return CliExitCodes.Success;
        case "enable":
            await SetSplitDnsEnabledAsync(host, enabled: true).ConfigureAwait(false);
            Console.WriteLine("Split DNS enabled in configuration. Run 'dns-switcher split-dns apply' to apply NRPT rules.");
            return CliExitCodes.Success;
        case "disable":
            await SetSplitDnsEnabledAsync(host, enabled: false).ConfigureAwait(false);
            Console.WriteLine("Split DNS disabled in configuration. Run 'dns-switcher split-dns apply' or 'reset' to remove NRPT rules.");
            return CliExitCodes.Success;
        case "add":
            return await ExecuteSplitDnsAddAsync(host, args).ConfigureAwait(false);
        case "remove":
        case "delete":
            return await ExecuteSplitDnsRemoveAsync(host, args).ConfigureAwait(false);
        case "update":
        case "edit":
            return await ExecuteSplitDnsUpdateAsync(host, args).ConfigureAwait(false);
        case "enable-rule":
            return await ExecuteSplitDnsSetRuleEnabledAsync(host, args, enabled: true).ConfigureAwait(false);
        case "disable-rule":
            return await ExecuteSplitDnsSetRuleEnabledAsync(host, args, enabled: false).ConfigureAwait(false);
        case "test":
            return await ExecuteSplitDnsTestAsync(host, args).ConfigureAwait(false);
        case "apply":
            return await ExecuteSplitDnsApplyAsync(host).ConfigureAwait(false);
        case "reset":
            await host.AgentSplitDnsService.ResetAsync().ConfigureAwait(false);
            Console.WriteLine("DnsSwitcher Split DNS NRPT rules were removed.");
            return CliExitCodes.Success;
        default:
            Console.Error.WriteLine("Usage: dns-switcher split-dns <status|enable|disable|list|add|remove|update|enable-rule|disable-rule|test|apply|reset> [args]");
            return CliExitCodes.InvalidArguments;
    }
}

static async Task PrintSplitDnsConfigurationAsync(WindowsDnsSwitcherHost host)
{
    var configuration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(false);
    Console.WriteLine($"Enabled: {configuration.Enabled}");
    Console.WriteLine($"Mode: {configuration.Mode}");
    Console.WriteLine($"Default behavior: {configuration.DefaultBehavior}");
    Console.WriteLine($"Rules: {configuration.Rules.Count}");

    foreach (var rule in configuration.Rules
        .OrderByDescending(rule => rule.Priority)
        .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(
            $"  - {rule.Id}: {rule.Namespace} -> {rule.ProfileId} | " +
            $"enabled={rule.Enabled} priority={rule.Priority}" +
            $"{(string.IsNullOrWhiteSpace(rule.Comment) ? string.Empty : $" | {rule.Comment}")}");
    }
}

static async Task SetSplitDnsEnabledAsync(WindowsDnsSwitcherHost host, bool enabled)
{
    var configuration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(false);
    await host.SplitDnsRuleService.SaveConfigurationAsync(configuration with { Enabled = enabled }).ConfigureAwait(false);
}

static async Task<int> ExecuteSplitDnsAddAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count < 3)
    {
        Console.Error.WriteLine("Usage: dns-switcher split-dns add <domain-or-suffix> <profile-id> [comment]");
        return CliExitCodes.InvalidArguments;
    }

    var comment = args.Count > 3 ? string.Join(" ", args.Skip(3)) : null;
    var rule = await host.SplitDnsRuleService.AddRuleAsync(args[1], args[2], comment).ConfigureAwait(false);
    Console.WriteLine($"Added Split DNS rule '{rule.Id}': {rule.Namespace} -> {rule.ProfileId}");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteSplitDnsRemoveAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count != 2)
    {
        Console.Error.WriteLine("Usage: dns-switcher split-dns remove <rule-id-or-namespace>");
        return CliExitCodes.InvalidArguments;
    }

    await host.SplitDnsRuleService.RemoveRuleAsync(args[1]).ConfigureAwait(false);
    Console.WriteLine($"Removed Split DNS rule '{args[1]}'.");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteSplitDnsUpdateAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count != 4)
    {
        Console.Error.WriteLine("Usage: dns-switcher split-dns update <rule-id> <domain-or-suffix> <profile-id>");
        return CliExitCodes.InvalidArguments;
    }

    var rule = await host.SplitDnsRuleService.UpdateRuleAsync(args[1], args[2], args[3]).ConfigureAwait(false);
    Console.WriteLine($"Updated Split DNS rule '{rule.Id}': {rule.Namespace} -> {rule.ProfileId}");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteSplitDnsSetRuleEnabledAsync(
    WindowsDnsSwitcherHost host,
    IReadOnlyList<string> args,
    bool enabled)
{
    if (args.Count != 2)
    {
        Console.Error.WriteLine($"Usage: dns-switcher split-dns {(enabled ? "enable-rule" : "disable-rule")} <rule-id>");
        return CliExitCodes.InvalidArguments;
    }

    await host.SplitDnsRuleService.SetRuleEnabledAsync(args[1], enabled).ConfigureAwait(false);
    Console.WriteLine($"Split DNS rule '{args[1]}' {(enabled ? "enabled" : "disabled")}.");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteSplitDnsTestAsync(WindowsDnsSwitcherHost host, IReadOnlyList<string> args)
{
    if (args.Count != 2)
    {
        Console.Error.WriteLine("Usage: dns-switcher split-dns test <domain>");
        return CliExitCodes.InvalidArguments;
    }

    var match = await host.SplitDnsRuleService.TestMatchAsync(args[1]).ConfigureAwait(false);
    Console.WriteLine($"Domain: {match.Domain}");
    Console.WriteLine($"Matched: {match.Matched}");
    Console.WriteLine($"Rule: {match.Rule?.Id ?? "<none>"}");
    Console.WriteLine($"Details: {match.Details}");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteSplitDnsApplyAsync(WindowsDnsSwitcherHost host)
{
    var configuration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(false);
    await host.AgentSplitDnsService.ApplyAsync(configuration).ConfigureAwait(false);
    Console.WriteLine("Split DNS NRPT rules applied.");
    return CliExitCodes.Success;
}

static async Task<int> ApplyProfileAsync(WindowsDnsSwitcherHost host, string? profileId, string? adapterSelection)
{
    if (string.IsNullOrWhiteSpace(profileId))
    {
        Console.Error.WriteLine("Profile id is required. Usage: dns-switcher apply <profile-id>");
        return CliExitCodes.InvalidArguments;
    }

    var profile = await host.ProfileService.GetRequiredProfileAsync(profileId).ConfigureAwait(false);
    var warnings = await BuildApplyWarningsAsync(host, profile, adapterSelection).ConfigureAwait(false);

    await host.AgentDnsSwitchService.ApplyProfileAsync(profileId, adapterSelection).ConfigureAwait(false);
    Console.WriteLine($"Applied DNS profile '{profileId}' to adapter '{adapterSelection ?? "<auto>"}'.");
    foreach (var warning in warnings)
    {
        Console.WriteLine($"Warning: {FormatApplyWarning(warning)}");
    }

    return CliExitCodes.Success;
}

static async Task<IReadOnlyList<DnsApplyWarning>> BuildApplyWarningsAsync(
    WindowsDnsSwitcherHost host,
    DnsProfile profile,
    string? adapterSelection)
{
    var adapter = await host.NetworkAdapterService.GetSelectedAdapterAsync(adapterSelection).ConfigureAwait(false);
    return adapter is null
        ? []
        : DnsApplyWarningBuilder.Build(profile, adapter);
}

static string FormatApplyWarning(DnsApplyWarning warning)
{
    return warning.Kind switch
    {
        DnsApplyWarningKind.UnsupportedIpv4Skipped =>
            $"Adapter '{warning.AdapterName}' has IPv4 disabled or unsupported. IPv4 DNS servers from profile '{warning.ProfileName}' were skipped.",
        DnsApplyWarningKind.UnsupportedIpv6Skipped =>
            $"Adapter '{warning.AdapterName}' has IPv6 disabled or unsupported. IPv6 DNS servers from profile '{warning.ProfileName}' were skipped; IPv4 DNS was applied when available.",
        _ => "Some DNS servers from the profile were skipped because the adapter does not support that IP stack.",
    };
}

static async Task<int> ResetToDhcpAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    await host.AgentDnsSwitchService.ResetToDhcpAsync(adapterSelection).ConfigureAwait(false);
    Console.WriteLine($"DNS settings were reset to DHCP for adapter '{adapterSelection ?? "<auto>"}'.");
    return CliExitCodes.Success;
}

static async Task<int> ExecuteServiceCommandAsync(
    WindowsDnsSwitcherHost host,
    string? serviceCommand,
    string? agentPath)
{
    if (string.IsNullOrWhiteSpace(serviceCommand))
    {
        Console.Error.WriteLine("Service command is required. Usage: dns-switcher service <install|reinstall|uninstall|start|stop|status> [agent-exe-path]");
        return CliExitCodes.InvalidArguments;
    }

    switch (serviceCommand.Trim().ToLowerInvariant())
    {
        case "install":
            var installInfo = await host.AgentServiceManager.GetInfoAsync().ConfigureAwait(false);
            if (installInfo.IsInstalled)
            {
                Console.WriteLine("DnsSwitcher Agent service is already installed.");
                if (installInfo.IsStalePath)
                {
                    Console.WriteLine("Warning: service points to a stale path. Run 'dns-switcher service reinstall'.");
                }

                return CliExitCodes.Success;
            }

            await host.AgentServiceManager.InstallAsync(agentPath).ConfigureAwait(false);
            Console.WriteLine($"DnsSwitcher Agent service installed{(string.IsNullOrWhiteSpace(agentPath) ? string.Empty : $" from '{agentPath}'")}.");
            return CliExitCodes.Success;
        case "reinstall":
            await ReinstallServiceAsync(host, agentPath).ConfigureAwait(false);
            Console.WriteLine("DnsSwitcher Agent service reinstalled and started.");
            return CliExitCodes.Success;
        case "uninstall":
            await host.AgentServiceManager.UninstallAsync().ConfigureAwait(false);
            Console.WriteLine("DnsSwitcher Agent service uninstalled.");
            return CliExitCodes.Success;
        case "start":
            await host.AgentServiceManager.StartAsync().ConfigureAwait(false);
            Console.WriteLine("DnsSwitcher Agent service started.");
            return CliExitCodes.Success;
        case "stop":
            await host.AgentServiceManager.StopAsync().ConfigureAwait(false);
            Console.WriteLine("DnsSwitcher Agent service stopped.");
            return CliExitCodes.Success;
        case "status":
            var info = await host.AgentServiceManager.GetInfoAsync().ConfigureAwait(false);
            Console.WriteLine($"DnsSwitcher Agent service status: {info.Status}");
            Console.WriteLine($"Service binary path: {info.ServiceBinaryPath ?? "<not installed>"}");
            Console.WriteLine($"Expected binary path: {info.ExpectedBinaryPath}");
            Console.WriteLine($"Path current: {info.PointsToExpectedPath}");
            if (info.IsStalePath)
            {
                Console.WriteLine("Warning: service points to a stale path. Run 'dns-switcher service reinstall'.");
            }
            return CliExitCodes.Success;
        default:
            Console.Error.WriteLine($"Unknown service command: {serviceCommand}");
            Console.Error.WriteLine("Usage: dns-switcher service <install|reinstall|uninstall|start|stop|status> [agent-exe-path]");
            return CliExitCodes.InvalidArguments;
    }
}

static async Task ReinstallServiceAsync(WindowsDnsSwitcherHost host, string? agentPath)
{
    var status = await host.AgentServiceManager.GetStatusAsync().ConfigureAwait(false);

    if (status is AgentServiceStatus.Running or AgentServiceStatus.StartPending)
    {
        await host.AgentServiceManager.StopAsync().ConfigureAwait(false);
    }

    if (status != AgentServiceStatus.NotInstalled)
    {
        await host.AgentServiceManager.UninstallAsync().ConfigureAwait(false);
    }

    await host.AgentServiceManager.InstallAsync(agentPath).ConfigureAwait(false);
    await host.AgentServiceManager.StartAsync().ConfigureAwait(false);
}

static async Task PrintAdaptersAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    var adapters = await host.NetworkAdapterService.GetAdaptersAsync().ConfigureAwait(false);
    var selectedAdapter = await GetSelectedAdapterAsync(host, adapterSelection).ConfigureAwait(false);

    if (adapters.Count == 0)
    {
        Console.WriteLine("No network adapters detected.");
        return;
    }

    foreach (var adapter in adapters.OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase))
    {
        var selectedMarker = selectedAdapter?.Id == adapter.Id ? "*" : " ";
        Console.WriteLine($"{selectedMarker} {adapter.Name}");
        Console.WriteLine($"    Id: {adapter.Id}");
        Console.WriteLine($"    Active: {adapter.IsActive}");
        Console.WriteLine($"    Physical: {adapter.IsPhysical}");
        Console.WriteLine($"    Loopback: {adapter.IsLoopback}");
        Console.WriteLine($"    Gateway: {adapter.HasDefaultGateway}");
        Console.WriteLine($"    Stacks: {adapter.SupportedStacks}");

        if (adapter.InterfaceIndex is not null)
        {
            Console.WriteLine($"    Interface index: {adapter.InterfaceIndex}");
        }
    }
}

static WindowsDnsSwitcherHost CreateHost(CliInvocation invocation)
{
    return WindowsDnsSwitcherHostFactory.Create(invocation.ConfigPath);
}

static async Task<int> ExecuteCommandWithHandlingAsync(
    WindowsDnsSwitcherHost host,
    CliInvocation invocation,
    bool interactive)
{
    var logger = host.LoggerFactory.CreateLogger("DnsSwitcher.Cli");

    try
    {
        return await ExecuteCommandCoreAsync(host, invocation).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "CLI command {Command} failed.", invocation.Command?.ToString() ?? "<interactive>");
        return HandleException(exception, interactive);
    }
}

static async Task<int> ExecuteCommandCoreAsync(WindowsDnsSwitcherHost host, CliInvocation invocation)
{
    return invocation.Command switch
    {
        CliCommand.Profiles => await ExecuteAndReturnSuccessAsync(() => PrintProfilesAsync(host)).ConfigureAwait(false),
        CliCommand.Adapters => await ExecuteAndReturnSuccessAsync(() => PrintAdaptersAsync(host, invocation.AdapterSelection)).ConfigureAwait(false),
        CliCommand.Status => await ExecuteAndReturnSuccessAsync(() => PrintStatusAsync(host, invocation.AdapterSelection)).ConfigureAwait(false),
        CliCommand.Test => await ExecuteAndReturnSuccessAsync(() => PrintDnsTestAsync(host, invocation.AdapterSelection)).ConfigureAwait(false),
        CliCommand.TestSites => await ExecuteAndReturnSuccessAsync(() => PrintSiteConnectivityTestAsync(host, invocation.AdapterSelection)).ConfigureAwait(false),
        CliCommand.Benchmark => await ExecuteAndReturnSuccessAsync(() => PrintBenchmarkAsync(host, invocation.AdapterSelection)).ConfigureAwait(false),
        CliCommand.Health => await ExecuteHealthCommandAsync(host, invocation).ConfigureAwait(false),
        CliCommand.SplitDns => await ExecuteSplitDnsCommandAsync(host, invocation).ConfigureAwait(false),
        CliCommand.Apply => await ApplyProfileAsync(host, invocation.CommandArgument, invocation.AdapterSelection).ConfigureAwait(false),
        CliCommand.Reset => await ResetToDhcpAsync(host, invocation.AdapterSelection).ConfigureAwait(false),
        CliCommand.ValidateConfig => await ExecuteAndReturnSuccessAsync(() => ValidateConfigAsync(host)).ConfigureAwait(false),
        CliCommand.Service => await ExecuteServiceCommandAsync(host, invocation.CommandArgument, invocation.SecondaryArgument).ConfigureAwait(false),
        CliCommand.Help => ExecuteHelp(),
        null => CliExitCodes.Success,
        _ => throw new InvalidOperationException($"Unsupported command: {invocation.Command}"),
    };
}

static async Task<int> ExecuteAndReturnSuccessAsync(Func<Task> action)
{
    await action().ConfigureAwait(false);
    return CliExitCodes.Success;
}

static int ExecuteHelp()
{
    PrintHelp();
    return CliExitCodes.Success;
}

static int HandleException(Exception exception, bool interactive)
{
    var exitCode = exception switch
    {
        AppConfigValidationException => CliExitCodes.InvalidConfig,
        InvalidDataException => CliExitCodes.InvalidConfig,
        DnsProfileNotFoundException => CliExitCodes.ProfileNotFound,
        NetworkAdapterNotFoundException => CliExitCodes.AdapterError,
        NetworkAdapterDisabledException => CliExitCodes.AdapterError,
        DnsAgentUnavailableException => CliExitCodes.DnsOperationFailed,
        DnsOperationRequiresAdminException => CliExitCodes.AdminRequired,
        DnsOperationFailedException => CliExitCodes.DnsOperationFailed,
        _ => CliExitCodes.UnexpectedError,
    };

    WriteException(exception, interactive);
    return exitCode;
}

static void WriteException(Exception exception, bool interactive)
{
    var writer = Console.Error;

    if (exception is AppConfigValidationException validationException)
    {
        writer.WriteLine("profiles.json is invalid:");

        foreach (var error in validationException.Errors)
        {
            writer.WriteLine($"  - {error.Path}: {error.Message} ({error.Code})");
        }

        return;
    }

    if (exception is InvalidDataException)
    {
        writer.WriteLine(exception.Message);
        return;
    }

    if (exception is DnsProfileNotFoundException
        or NetworkAdapterNotFoundException
        or NetworkAdapterDisabledException
        or DnsAgentUnavailableException
        or DnsOperationRequiresAdminException
        or DnsOperationFailedException)
    {
        writer.WriteLine(exception.Message);
        return;
    }

    writer.WriteLine(interactive
        ? $"Unexpected error: {exception.Message}"
        : $"Unexpected error: {exception.Message}");
}

static async Task<int> RunInteractiveAsync(CliInvocation sessionInvocation)
{
    using var host = CreateHost(sessionInvocation);
    await host.ProfileService.EnsureInitializedAsync().ConfigureAwait(false);

    while (true)
    {
        Console.Clear();
        PrintInteractiveHeader(host, sessionInvocation);

        var choice = ReadInput("Select action");

        if (string.Equals(choice, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(choice, "q", StringComparison.OrdinalIgnoreCase)
            || string.Equals(choice, "quit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(choice, "exit", StringComparison.OrdinalIgnoreCase))
        {
            return CliExitCodes.Success;
        }

        var commandInvocation = await CreateInteractiveInvocationAsync(host, sessionInvocation, choice).ConfigureAwait(false);

        if (commandInvocation is null)
        {
            PauseInteractive();
            continue;
        }

        var exitCode = await ExecuteCommandWithHandlingAsync(host, commandInvocation, interactive: true).ConfigureAwait(false);

        if (exitCode != CliExitCodes.Success)
        {
            Console.WriteLine();
            Console.WriteLine($"Command finished with exit code {exitCode}.");
        }

        PauseInteractive();
    }
}

static void PrintInteractiveHeader(WindowsDnsSwitcherHost host, CliInvocation sessionInvocation)
{
    Console.WriteLine("DnsSwitcher Console");
    Console.WriteLine();
    Console.WriteLine($"Profiles file: {host.Paths.ProfilesFilePath}");
    Console.WriteLine($"Adapter target: {sessionInvocation.AdapterSelection ?? "<auto-select>"}");
    Console.WriteLine();
    Console.WriteLine("1. Profiles");
    Console.WriteLine("2. Adapters");
    Console.WriteLine("3. Status");
    Console.WriteLine("4. Apply profile");
    Console.WriteLine("5. Reset to DHCP");
    Console.WriteLine("6. Test current DNS");
    Console.WriteLine("7. Test sites");
    Console.WriteLine("8. Benchmark profiles");
    Console.WriteLine("9. Health status");
    Console.WriteLine("10. Validate config");
    Console.WriteLine("11. Agent service status");
    Console.WriteLine("12. Health check");
    Console.WriteLine("13. Split DNS status");
    Console.WriteLine("14. Apply Split DNS");
    Console.WriteLine("15. Reset Split DNS");
    Console.WriteLine("0. Exit");
    Console.WriteLine();
}

static async Task<CliInvocation?> CreateInteractiveInvocationAsync(
    WindowsDnsSwitcherHost host,
    CliInvocation sessionInvocation,
    string? choice)
{
    return choice?.Trim() switch
    {
        "1" => sessionInvocation with { Command = CliCommand.Profiles },
        "2" => sessionInvocation with { Command = CliCommand.Adapters },
        "3" => sessionInvocation with { Command = CliCommand.Status },
        "4" => await CreateApplyInvocationAsync(host, sessionInvocation).ConfigureAwait(false),
        "5" => sessionInvocation with { Command = CliCommand.Reset },
        "6" => sessionInvocation with { Command = CliCommand.Test },
        "7" => sessionInvocation with { Command = CliCommand.TestSites },
        "8" => sessionInvocation with { Command = CliCommand.Benchmark },
        "9" => sessionInvocation with { Command = CliCommand.Health, CommandArgument = "status", AdditionalArguments = ["status"] },
        "10" => sessionInvocation with { Command = CliCommand.ValidateConfig },
        "11" => sessionInvocation with { Command = CliCommand.Service, CommandArgument = "status", AdditionalArguments = ["status"] },
        "12" => sessionInvocation with { Command = CliCommand.Health, CommandArgument = "check", AdditionalArguments = ["check"] },
        "13" => sessionInvocation with { Command = CliCommand.SplitDns, CommandArgument = "status", AdditionalArguments = ["status"] },
        "14" => sessionInvocation with { Command = CliCommand.SplitDns, CommandArgument = "apply", AdditionalArguments = ["apply"] },
        "15" => sessionInvocation with { Command = CliCommand.SplitDns, CommandArgument = "reset", AdditionalArguments = ["reset"] },
        _ => InvalidInteractiveChoice(),
    };
}

static CliInvocation? InvalidInteractiveChoice()
{
    Console.WriteLine("Unknown menu choice.");
    return null;
}

static async Task<CliInvocation?> CreateApplyInvocationAsync(WindowsDnsSwitcherHost host, CliInvocation sessionInvocation)
{
    var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(false);

    if (configuration.Profiles.Count == 0)
    {
        Console.WriteLine("No DNS profiles configured.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine("Available profiles:");

    for (var index = 0; index < configuration.Profiles.Count; index++)
    {
        var profile = configuration.Profiles[index];
        Console.WriteLine($"{index + 1}. {profile.Name} ({profile.Id}) - {profile.Mode}");
    }

    Console.WriteLine();
    var rawValue = ReadInput("Enter profile number or id (blank to cancel)");

    if (string.IsNullOrWhiteSpace(rawValue))
    {
        Console.WriteLine("Apply was cancelled.");
        return null;
    }

    var profileId = TryResolveProfileId(configuration.Profiles, rawValue);

    if (profileId is null)
    {
        Console.WriteLine($"Profile '{rawValue}' was not found.");
        return null;
    }

    return sessionInvocation with
    {
        Command = CliCommand.Apply,
        CommandArgument = profileId,
    };
}

static string? TryResolveProfileId(IReadOnlyList<DnsProfile> profiles, string rawValue)
{
    if (int.TryParse(rawValue, out var index) && index >= 1 && index <= profiles.Count)
    {
        return profiles[index - 1].Id;
    }

    return profiles.FirstOrDefault(profile =>
        string.Equals(profile.Id, rawValue, StringComparison.OrdinalIgnoreCase))?.Id;
}

static string ReadInput(string label)
{
    Console.Write($"{label}: ");
    return Console.ReadLine()?.Trim() ?? string.Empty;
}

static void PauseInteractive()
{
    Console.WriteLine();
    Console.Write("Press Enter to continue...");
    Console.ReadLine();
}

static void TryConfigureConsoleEncoding()
{
    try
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
    }
    catch (IOException)
    {
    }
    catch (InvalidOperationException)
    {
    }
}

static async Task<DnsSwitcher.Core.Models.NetworkAdapter?> GetSelectedAdapterAsync(
    WindowsDnsSwitcherHost host,
    string? adapterSelection)
{
    var adapter = await host.NetworkAdapterService.GetSelectedAdapterAsync(adapterSelection).ConfigureAwait(false);

    if (adapter is null && !string.IsNullOrWhiteSpace(adapterSelection))
    {
        throw new NetworkAdapterNotFoundException($"Network adapter '{adapterSelection}' was not found.");
    }

    return adapter;
}
