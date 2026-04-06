using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using Microsoft.Extensions.Logging;

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

        case "validate":
            await ValidateProfilesAsync(host).ConfigureAwait(false);
            return 0;

        case "switch":
        case "enable":
        case "disable":
            Console.Error.WriteLine($"Command '{command}' is planned after v0.1 and is not implemented yet.");
            return 2;

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
          dns-switcher status   Show current skeleton DNS status
          dns-switcher validate Validate profiles.json

        Planned:
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
    Console.WriteLine($"System managed by app: {dnsStatus.IsManaged}");
    Console.WriteLine($"System DNS details: {dnsStatus.Details}");
}

static async Task ValidateProfilesAsync(WindowsDnsSwitcherHost host)
{
    _ = await host.ProfileStore.LoadAsync().ConfigureAwait(false);
    Console.WriteLine($"profiles.json is valid: {host.Paths.ProfilesFilePath}");
}
