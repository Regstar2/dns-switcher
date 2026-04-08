using System.Runtime.Versioning;
using DnsSwitcher.Cli;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
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
    await host.ProfileService.EnsureInitializedAsync().ConfigureAwait(false);

    return await ExecuteCommandWithHandlingAsync(host, invocation, interactive: false).ConfigureAwait(false);
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
          dns-switcher apply <profile-id>
          dns-switcher reset
          dns-switcher validate-config

        Options:
          --adapter <id|name>   Use a specific adapter instead of auto-selection
          --config <path>       Use a custom profiles.json path or config directory
          -h, --help            Show this help

        Legacy aliases:
          list -> profiles
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

    Console.WriteLine($"Portable data: {host.Paths.AppDirectory}");
    Console.WriteLine($"Profiles file: {host.Paths.ProfilesFilePath}");
    Console.WriteLine($"Adapter override: {adapterSelection ?? "<auto>"}");
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

static async Task<int> ApplyProfileAsync(WindowsDnsSwitcherHost host, string? profileId, string? adapterSelection)
{
    if (string.IsNullOrWhiteSpace(profileId))
    {
        Console.Error.WriteLine("Profile id is required. Usage: dns-switcher apply <profile-id>");
        return CliExitCodes.InvalidArguments;
    }

    await host.DnsSwitchService.ApplyProfileAsync(profileId, adapterSelection).ConfigureAwait(false);
    Console.WriteLine($"Applied DNS profile '{profileId}' to adapter '{adapterSelection ?? "<auto>"}'.");
    return CliExitCodes.Success;
}

static async Task<int> ResetToDhcpAsync(WindowsDnsSwitcherHost host, string? adapterSelection)
{
    await host.DnsSwitchService.ResetToDhcpAsync(adapterSelection).ConfigureAwait(false);
    Console.WriteLine($"DNS settings were reset to DHCP for adapter '{adapterSelection ?? "<auto>"}'.");
    return CliExitCodes.Success;
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
    var paths = string.IsNullOrWhiteSpace(invocation.ConfigPath)
        ? PortableAppPaths.CreateDefault()
        : PortableAppPaths.CreateFromConfigPath(invocation.ConfigPath);

    paths.EnsureDirectories();

    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddProvider(new FileLoggerProvider(paths.LogFilePath));
    });

    return new WindowsDnsSwitcherHost(paths, loggerFactory);
}

static async Task<int> ExecuteCommandWithHandlingAsync(
    WindowsDnsSwitcherHost host,
    CliInvocation invocation,
    bool interactive)
{
    try
    {
        return await ExecuteCommandCoreAsync(host, invocation).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
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
        CliCommand.Apply => await ApplyProfileAsync(host, invocation.CommandArgument, invocation.AdapterSelection).ConfigureAwait(false),
        CliCommand.Reset => await ResetToDhcpAsync(host, invocation.AdapterSelection).ConfigureAwait(false),
        CliCommand.ValidateConfig => await ExecuteAndReturnSuccessAsync(() => ValidateConfigAsync(host)).ConfigureAwait(false),
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
    Console.WriteLine("6. Validate config");
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
        "6" => sessionInvocation with { Command = CliCommand.ValidateConfig },
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
