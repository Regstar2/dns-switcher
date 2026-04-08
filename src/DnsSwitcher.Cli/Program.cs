using System.Runtime.Versioning;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using Microsoft.Extensions.Logging;

[assembly: SupportedOSPlatform("windows")]

try
{
    var exitCode = await RunAsync(args).ConfigureAwait(false);
    return exitCode;
}
catch (AppConfigValidationException exception)
{
    Console.Error.WriteLine("profiles.json is invalid:");

    foreach (var error in exception.Errors)
    {
        Console.Error.WriteLine($"  - {error.Path}: {error.Message} ({error.Code})");
    }

    return 3;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 3;
}
catch (DnsProfileNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 4;
}
catch (NetworkAdapterNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 5;
}
catch (NetworkAdapterDisabledException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 5;
}
catch (DnsOperationRequiresAdminException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 6;
}
catch (DnsOperationFailedException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 7;
}

static async Task<int> RunAsync(string[] args)
{
    var paths = PortableAppPaths.CreateDefault();
    paths.EnsureDirectories();

    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddProvider(new FileLoggerProvider(paths.LogFilePath));
    });

    using var host = new WindowsDnsSwitcherHost(paths, loggerFactory);
    await host.ProfileService.EnsureInitializedAsync().ConfigureAwait(false);

    var command = args.FirstOrDefault()?.ToLowerInvariant();

    switch (command)
    {
        case null:
        case "":
        case "-h":
        case "--help":
        case "help":
            PrintHelp();
            return 0;

        case "paths":
            PrintPaths(host.Paths);
            return 0;

        case "init":
            await host.ProfileStore.EnsureCreatedAsync().ConfigureAwait(false);
            Console.WriteLine($"profiles.json is ready: {host.Paths.ProfilesFilePath}");
            return 0;

        case "list":
            await PrintProfilesAsync(host).ConfigureAwait(false);
            return 0;

        case "status":
            await PrintStatusAsync(host).ConfigureAwait(false);
            return 0;

        case "adapters":
            await PrintAdaptersAsync(host).ConfigureAwait(false);
            return 0;

        case "validate":
            await ValidateProfilesAsync(host).ConfigureAwait(false);
            return 0;

        case "switch":
        case "enable":
            return await ApplyProfileAsync(host, args.Skip(1).FirstOrDefault()).ConfigureAwait(false);

        case "disable":
            return await ResetToDhcpAsync(host).ConfigureAwait(false);

        default:
            Console.Error.WriteLine($"Unknown command: {command}");
            PrintHelp();
            return 1;
    }
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        DnsSwitcher CLI

        Usage:
          dns-switcher paths    Show portable config/log paths
          dns-switcher init     Create profiles.json if it does not exist
          dns-switcher list     List configured DNS profiles
          dns-switcher adapters List detected network adapters
          dns-switcher status   Show current DNS status
          dns-switcher validate Validate profiles.json
          dns-switcher switch <profile-id>
          dns-switcher enable <profile-id>
          dns-switcher disable
        """);
}

static void PrintPaths(PortableAppPaths paths)
{
    Console.WriteLine($"Data:     {paths.AppDirectory}");
    Console.WriteLine($"Config:   {paths.ConfigDirectory}");
    Console.WriteLine($"Profiles: {paths.ProfilesFilePath}");
    Console.WriteLine($"Logs:     {paths.LogDirectory}");
    Console.WriteLine($"Log file: {paths.LogFilePath}");
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
        Console.WriteLine($"    IPv4: {string.Join(", ", profile.Ipv4)}");

        if (profile.Ipv6.Count > 0)
        {
            Console.WriteLine($"    IPv6: {string.Join(", ", profile.Ipv6)}");
        }
    }
}

static async Task PrintStatusAsync(WindowsDnsSwitcherHost host)
{
    var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(false);
    var activeProfile = await host.ProfileService.GetActiveProfileAsync().ConfigureAwait(false);
    var dnsStatus = await host.DnsManager.GetStatusAsync().ConfigureAwait(false);

    Console.WriteLine($"Portable data: {host.Paths.AppDirectory}");
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

static async Task ValidateProfilesAsync(WindowsDnsSwitcherHost host)
{
    _ = await host.ProfileStore.LoadAsync().ConfigureAwait(false);
    Console.WriteLine($"profiles.json is valid: {host.Paths.ProfilesFilePath}");
}

static async Task<int> ApplyProfileAsync(WindowsDnsSwitcherHost host, string? profileId)
{
    if (string.IsNullOrWhiteSpace(profileId))
    {
        Console.Error.WriteLine("Profile id is required. Usage: dns-switcher switch <profile-id>");
        return 1;
    }

    await host.DnsSwitchService.ApplyProfileAsync(profileId).ConfigureAwait(false);
    Console.WriteLine($"Applied DNS profile: {profileId}");
    return 0;
}

static async Task<int> ResetToDhcpAsync(WindowsDnsSwitcherHost host)
{
    await host.DnsSwitchService.ResetToDhcpAsync().ConfigureAwait(false);
    Console.WriteLine("DNS settings were reset to DHCP.");
    return 0;
}

static async Task PrintAdaptersAsync(WindowsDnsSwitcherHost host)
{
    var adapters = await host.NetworkAdapterService.GetAdaptersAsync().ConfigureAwait(false);
    var selectedAdapter = await host.NetworkAdapterService.GetDefaultAdapterAsync().ConfigureAwait(false);

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
