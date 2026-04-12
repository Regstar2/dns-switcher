using System.ComponentModel;
using System.Diagnostics;
using DnsSwitcher.Contracts;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Infrastructure.Windows.Security;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed class WindowsAgentServiceManager(ILogger<WindowsAgentServiceManager> logger) : IAgentServiceManager
{
    public async Task InstallAsync(string? agentExecutablePath = null, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();

        var sourceExecutablePath = ResolveSourceAgentExecutablePath(agentExecutablePath);

        if (!File.Exists(sourceExecutablePath))
        {
            throw new FileNotFoundException($"DnsSwitcher Agent executable was not found: {sourceExecutablePath}", sourceExecutablePath);
        }

        if (await GetStatusAsync(cancellationToken).ConfigureAwait(false) != AgentServiceStatus.NotInstalled)
        {
            throw new DnsOperationFailedException("DnsSwitcher Agent service is already installed.");
        }

        var deployedExecutablePath = DeployAgentRuntime(sourceExecutablePath);

        await RunScAsync(
            $"create {AgentProtocol.ServiceName} binPath= {Quote(deployedExecutablePath)} start= auto DisplayName= {Quote(AgentProtocol.DisplayName)}",
            "install DnsSwitcher Agent service",
            cancellationToken).ConfigureAwait(false);

        await RunScAsync(
            $"description {AgentProtocol.ServiceName} {Quote("Privileged DNS operations for DnsSwitcher.")}",
            "configure DnsSwitcher Agent service description",
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Installed DnsSwitcher Agent service from {SourceExecutablePath} to {DeployedExecutablePath}.",
            sourceExecutablePath,
            deployedExecutablePath);
    }

    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();

        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);

        if (status == AgentServiceStatus.NotInstalled)
        {
            return;
        }

        if (status is AgentServiceStatus.Running or AgentServiceStatus.StartPending)
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);
        }

        await RunScAsync($"delete {AgentProtocol.ServiceName}", "remove DnsSwitcher Agent service", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        await RunScAsync($"start {AgentProtocol.ServiceName}", "start DnsSwitcher Agent service", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        await RunScAsync($"stop {AgentProtocol.ServiceName}", "stop DnsSwitcher Agent service", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AgentServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunProcessAsync("sc.exe", $"query {AgentProtocol.ServiceName}", cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            if (result.ExitCode == 1060
                || result.Output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase)
                || result.Error.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase))
            {
                return AgentServiceStatus.NotInstalled;
            }

            throw new DnsOperationFailedException(
                $"Failed to query DnsSwitcher Agent service status. Details: {GetDetails(result)}");
        }

        var text = string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output;

        if (text.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return AgentServiceStatus.Running;
        }

        if (text.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return AgentServiceStatus.StopPending;
        }

        if (text.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return AgentServiceStatus.StartPending;
        }

        if (text.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
        {
            return AgentServiceStatus.Stopped;
        }

        return AgentServiceStatus.Unknown;
    }

    private static void EnsureAdministrator()
    {
        if (!WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsOperationRequiresAdminException();
        }
    }

    private static string ResolveSourceAgentExecutablePath(string? agentExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(agentExecutablePath))
        {
            return Path.GetFullPath(agentExecutablePath);
        }

        foreach (var candidate in GetDefaultAgentExecutableCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return GetDefaultAgentExecutableCandidates().First();
    }

    private static string DeployAgentRuntime(string sourceExecutablePath)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceExecutablePath)
            ?? throw new InvalidOperationException("Agent source directory could not be determined.");
        var deploymentDirectory = AgentDeploymentLayout.GetDeploymentDirectory(AppContext.BaseDirectory);

        Directory.CreateDirectory(deploymentDirectory);
        CopyDirectory(sourceDirectory, deploymentDirectory);

        return Path.Combine(deploymentDirectory, Path.GetFileName(sourceExecutablePath));
    }

    private static IReadOnlyList<string> GetDefaultAgentExecutableCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var applicationRoot = AgentDeploymentLayout.GetApplicationRoot(baseDirectory);

        return
        [
            Path.GetFullPath(Path.Combine(baseDirectory, "DnsSwitcher.Agent.Windows.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "DnsSwitcher.Agent.Windows", "DnsSwitcher.Agent.Windows.exe")),
            Path.GetFullPath(Path.Combine(applicationRoot, "agent", "DnsSwitcher.Agent.Windows.exe")),
            Path.GetFullPath(Path.Combine(applicationRoot, "src", "DnsSwitcher.Agent.Windows", "bin", "Release", "net10.0-windows", "DnsSwitcher.Agent.Windows.exe")),
            Path.GetFullPath(Path.Combine(applicationRoot, "src", "DnsSwitcher.Agent.Windows", "bin", "Debug", "net10.0-windows", "DnsSwitcher.Agent.Windows.exe")),
        ];
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private async Task RunScAsync(string arguments, string operationDescription, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync("sc.exe", arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new DnsOperationFailedException(
                $"Failed to {operationDescription}. Details: {GetDetails(result)}");
        }

        logger.LogInformation("sc.exe {Arguments}", arguments);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
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
                throw new DnsOperationFailedException($"Failed to start '{fileName}'.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new DnsOperationFailedException($"Failed to start '{fileName}'.", exception);
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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessResult(
            process.ExitCode,
            (await outputTask.ConfigureAwait(false)).Trim(),
            (await errorTask.ConfigureAwait(false)).Trim());
    }

    private static string GetDetails(ProcessResult result)
    {
        return !string.IsNullOrWhiteSpace(result.Error)
            ? result.Error
            : !string.IsNullOrWhiteSpace(result.Output)
                ? result.Output
                : "Unknown error.";
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
