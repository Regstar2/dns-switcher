using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(
        SemanticVersion currentVersion,
        UpdateChannel channel = UpdateChannel.Stable,
        CancellationToken cancellationToken = default);

    Task<string> DownloadAndVerifyInstallerAsync(
        UpdateInfo update,
        CancellationToken cancellationToken = default);

    void LaunchInstaller(string installerPath);
}
