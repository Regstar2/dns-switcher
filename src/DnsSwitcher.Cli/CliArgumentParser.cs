namespace DnsSwitcher.Cli;

public static class CliArgumentParser
{
    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? adapterSelection = null;
        string? configPath = null;
        var positionals = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];

            if (IsHelpToken(token))
            {
                return CliParseResult.Success(new CliInvocation(CliCommand.Help, null, adapterSelection, configPath));
            }

            if (TryReadOption(args, ref index, token, "--adapter", out var adapterValue, out var adapterError))
            {
                if (adapterError is not null)
                {
                    return CliParseResult.Failure(adapterError);
                }

                adapterSelection = adapterValue;
                continue;
            }

            if (TryReadOption(args, ref index, token, "--config", out var configValue, out var configError))
            {
                if (configError is not null)
                {
                    return CliParseResult.Failure(configError);
                }

                configPath = configValue;
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                return CliParseResult.Failure($"Unknown option: {token}");
            }

            positionals.Add(token);
        }

        if (positionals.Count == 0)
        {
            return CliParseResult.Success(new CliInvocation(null, null, adapterSelection, configPath));
        }

        if (!TryParseCommand(positionals[0], out var command))
        {
            return CliParseResult.Failure($"Unknown command: {positionals[0]}");
        }

        var commandArgument = positionals.Count > 1 ? positionals[1] : null;
        var secondaryArgument = positionals.Count > 2 ? positionals[2] : null;

        if (!ValidateArguments(command, positionals.Count, out var error))
        {
            return CliParseResult.Failure(error);
        }

        return CliParseResult.Success(new CliInvocation(command, commandArgument, adapterSelection, configPath, secondaryArgument));
    }

    private static bool IsHelpToken(string token)
    {
        return token is "-h" or "--help" or "help";
    }

    private static bool TryReadOption(
        IReadOnlyList<string> args,
        ref int index,
        string token,
        string optionName,
        out string? value,
        out string? error)
    {
        value = null;
        error = null;

        if (string.Equals(token, optionName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Count)
            {
                error = $"Option '{optionName}' requires a value.";
                return true;
            }

            index++;
            value = args[index];
            return true;
        }

        var prefix = $"{optionName}=";

        if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = token[prefix.Length..];

            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Option '{optionName}' requires a value.";
            }

            return true;
        }

        return false;
    }

    private static bool TryParseCommand(string token, out CliCommand command)
    {
        switch (token.ToLowerInvariant())
        {
            case "profiles":
            case "list":
                command = CliCommand.Profiles;
                return true;
            case "adapters":
                command = CliCommand.Adapters;
                return true;
            case "status":
                command = CliCommand.Status;
                return true;
            case "apply":
            case "switch":
            case "enable":
                command = CliCommand.Apply;
                return true;
            case "reset":
            case "disable":
                command = CliCommand.Reset;
                return true;
            case "validate-config":
            case "validate":
                command = CliCommand.ValidateConfig;
                return true;
            case "service":
                command = CliCommand.Service;
                return true;
            case "help":
                command = CliCommand.Help;
                return true;
            default:
                command = default;
                return false;
        }
    }

    private static bool ValidateArguments(CliCommand command, int positionalCount, out string error)
    {
        var isValid = command switch
        {
            CliCommand.Apply => positionalCount == 2,
            CliCommand.Service => positionalCount is 2 or 3,
            CliCommand.Help or CliCommand.Profiles or CliCommand.Adapters or CliCommand.Status
                or CliCommand.Reset or CliCommand.ValidateConfig => positionalCount == 1,
            _ => positionalCount == 1,
        };

        if (isValid)
        {
            error = string.Empty;
            return true;
        }

        error = command switch
        {
            CliCommand.Apply => "Usage: dns-switcher apply <profile-id> [--adapter <id|name>] [--config <path>]",
            CliCommand.Reset => "Usage: dns-switcher reset [--adapter <id|name>] [--config <path>]",
            CliCommand.Status => "Usage: dns-switcher status [--adapter <id|name>] [--config <path>]",
            CliCommand.Profiles => "Usage: dns-switcher profiles [--config <path>]",
            CliCommand.Adapters => "Usage: dns-switcher adapters [--adapter <id|name>] [--config <path>]",
            CliCommand.ValidateConfig => "Usage: dns-switcher validate-config [--config <path>]",
            CliCommand.Service => "Usage: dns-switcher service <install|uninstall|start|stop|status> [agent-exe-path]",
            _ => "Invalid command arguments.",
        };

        return false;
    }
}
