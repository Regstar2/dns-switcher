using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Security;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.SplitDns;

[SupportedOSPlatform("windows")]
public sealed class WindowsNrptSplitDnsManager(ILogger<WindowsNrptSplitDnsManager> logger) : ISplitDnsManager
{
    private const string DisplayNamePrefix = "DnsSwitcher Split DNS:";
    private const string CommentPrefix = "DnsSwitcher managed rule";

    public async Task ApplyAsync(
        SplitDnsConfiguration splitDnsConfiguration,
        AppConfig appConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(splitDnsConfiguration);
        ArgumentNullException.ThrowIfNull(appConfig);
        EnsureAdministrator();

        var enabledRules = splitDnsConfiguration.Enabled
            ? splitDnsConfiguration.Rules.Where(rule => rule.Enabled).ToArray()
            : [];

        if (enabledRules.Length == 0)
        {
            await ResetAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Split DNS is disabled or has no enabled rules. Existing DnsSwitcher NRPT rules were removed.");
            return;
        }

        var commands = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
            BuildResetScriptBody(),
        };

        foreach (var rule in enabledRules)
        {
            var profile = appConfig.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, rule.ProfileId, StringComparison.OrdinalIgnoreCase));

            if (profile is null)
            {
                throw new DnsOperationFailedException($"Split DNS rule '{rule.Id}' references missing profile '{rule.ProfileId}'.");
            }

            if (profile.Mode != ProfileMode.Static)
            {
                throw new DnsOperationFailedException($"Split DNS rule '{rule.Id}' requires a static DNS profile. Profile '{profile.Id}' is {profile.Mode}.");
            }

            var nameServers = profile.Ipv4
                .Concat(profile.Ipv6)
                .Where(server => !string.IsNullOrWhiteSpace(server))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (nameServers.Length == 0)
            {
                throw new DnsOperationFailedException($"Split DNS rule '{rule.Id}' profile '{profile.Id}' has no DNS servers.");
            }

            commands.Add(BuildAddRuleScript(rule, nameServers));
        }

        await ExecutePowerShellAsync(string.Join(Environment.NewLine, commands), "apply Split DNS NRPT rules", cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Applied Split DNS NRPT rules. Rule count: {RuleCount}.", enabledRules.Length);
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        var script = "$ErrorActionPreference = 'Stop'" + Environment.NewLine + BuildResetScriptBody();
        return ExecutePowerShellAsync(script, "reset Split DNS NRPT rules", cancellationToken);
    }

    private static string BuildResetScriptBody()
    {
        return
            "Get-DnsClientNrptRule | " +
            $"Where-Object {{ $_.DisplayName -like '{EscapePowerShellSingleQuoted(DisplayNamePrefix)}*' -or $_.Comment -like '{EscapePowerShellSingleQuoted(CommentPrefix)}*' }} | " +
            "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }";
    }

    private static string BuildAddRuleScript(SplitDnsRule rule, IReadOnlyList<string> nameServers)
    {
        var displayName = $"{DisplayNamePrefix} {rule.Id}";
        var comment = $"{CommentPrefix} {rule.Id}; profile {rule.ProfileId}";

        return
            "Add-DnsClientNrptRule " +
            $"-Namespace @({ToPowerShellArray([rule.Namespace])}) " +
            $"-NameServers @({ToPowerShellArray(nameServers)}) " +
            $"-DisplayName '{EscapePowerShellSingleQuoted(displayName)}' " +
            $"-Comment '{EscapePowerShellSingleQuoted(comment)}'";
    }

    private async Task ExecutePowerShellAsync(
        string script,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        try
        {
            if (!process.Start())
            {
                throw new DnsOperationFailedException($"Failed to start PowerShell to {operationDescription}.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new DnsOperationFailedException($"Failed to start PowerShell to {operationDescription}.", exception);
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        });

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var standardOutput = (await standardOutputTask.ConfigureAwait(false)).Trim();
        var standardError = (await standardErrorTask.ConfigureAwait(false)).Trim();

        if (process.ExitCode != 0)
        {
            var details = !string.IsNullOrWhiteSpace(standardError)
                ? standardError
                : !string.IsNullOrWhiteSpace(standardOutput)
                    ? standardOutput
                    : $"PowerShell exited with code {process.ExitCode}.";

            logger.LogWarning("PowerShell failed to {OperationDescription}. Details: {Details}", operationDescription, details);
            throw new DnsOperationFailedException($"Failed to {operationDescription}. Details: {details}");
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            logger.LogDebug("PowerShell output for {OperationDescription}: {Output}", operationDescription, standardOutput);
        }
    }

    private static string ToPowerShellArray(IReadOnlyList<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{EscapePowerShellSingleQuoted(value)}'"));
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static void EnsureAdministrator()
    {
        if (!WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsOperationRequiresAdminException();
        }
    }
}
