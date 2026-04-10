using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Desktop;

public sealed class DesktopClientLauncher(ILogger<DesktopClientLauncher> logger)
{
    public bool EnsureTrayRunning(string baseDirectory)
    {
        return EnsureProcessRunning("DnsSwitcher.Tray", DesktopClientLayout.TryGetTrayExecutablePath(baseDirectory), "tray");
    }

    public bool EnsureUiRunning(string baseDirectory)
    {
        return EnsureProcessRunning("DnsSwitcher.Ui", DesktopClientLayout.TryGetUiExecutablePath(baseDirectory), "UI");
    }

    private bool EnsureProcessRunning(string processName, string? executablePath, string clientDisplayName)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            logger.LogWarning("{ClientDisplayName} executable could not be found.", clientDisplayName);
            return false;
        }

        var desiredPath = Path.GetFullPath(executablePath);
        var runningProcesses = Process.GetProcessesByName(processName);

        if (runningProcesses.Any(process => IsSameExecutable(process, desiredPath)))
        {
            logger.LogInformation("{ClientDisplayName} client is already running from {ExecutablePath}.", clientDisplayName, desiredPath);
            return true;
        }

        if (runningProcesses.Length > 0)
        {
            logger.LogWarning(
                "{ClientDisplayName} process is already running, but not from the preferred path {ExecutablePath}. A new instance will be started.",
                clientDisplayName,
                desiredPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = desiredPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(desiredPath) ?? AppContext.BaseDirectory,
        });

        logger.LogInformation("Started {ClientDisplayName} client from {ExecutablePath}.", clientDisplayName, desiredPath);
        return true;
    }

    private static bool IsSameExecutable(Process process, string desiredPath)
    {
        try
        {
            var processPath = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(processPath)
                && string.Equals(Path.GetFullPath(processPath), desiredPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
