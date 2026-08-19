using System.Diagnostics;
using System.Windows;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Ui;

public partial class MainWindow
{
    private bool isCheckingForUpdates;

    private async Task CheckForUpdatesAsync(Window owner)
    {
        if (isCheckingForUpdates)
        {
            return;
        }

        isCheckingForUpdates = true;
        try
        {
            var result = await App.Host.UpdateService
                .CheckForUpdatesAsync(App.Host.ApplicationMetadata.Version, UpdateChannel.Stable)
                .ConfigureAwait(true);

            await RecordManualUpdateCheckAsync(result).ConfigureAwait(true);

            switch (result.Status)
            {
                case UpdateCheckStatus.Current:
                    MessageBox.Show(
                        owner,
                        localizer.FormatUpdateText("UpdateCurrentFormat", result.CurrentVersion),
                        localizer.GetUpdateText("UpdateDialogTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckStatus.UpdateAvailable when result.Update is not null:
                    ShowUpdateAvailableDialog(owner, result.Update);
                    break;

                default:
                    MessageBox.Show(
                        owner,
                        localizer.GetUpdateFailureText(result.FailureKind),
                        localizer.GetUpdateText("UpdateDialogTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Manual update check failed.");
            MessageBox.Show(
                owner,
                localizer.GetUpdateText("UpdateUnavailable"),
                localizer.GetUpdateText("UpdateDialogTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            isCheckingForUpdates = false;
        }
    }

    private void ShowUpdateAvailableDialog(Window owner, UpdateInfo update)
    {
        var updateWindow = new UpdateAvailableWindow(localizer, update)
        {
            Owner = owner,
        };

        updateWindow.ReleaseNotesRequested += (_, _) => OpenExternalUri(update.ReleasePageUri, updateWindow);
        updateWindow.InstallRequested += async (_, _) =>
        {
            updateWindow.SetBusy(true, localizer.GetUpdateText("UpdateDownloadingStatus"));
            try
            {
                var installerPath = await App.Host.UpdateService
                    .DownloadAndVerifyInstallerAsync(update)
                    .ConfigureAwait(true);
                updateWindow.SetBusy(true, localizer.GetUpdateText("UpdateVerifiedStatus"));
                App.Host.UpdateService.LaunchInstaller(installerPath);
                allowExplicitClose = true;
                Application.Current.Shutdown();
            }
            catch (UpdateDeliveryException exception)
            {
                logger.LogWarning(exception, "Update delivery failed with {UpdateFailureKind}.", exception.Kind);
                updateWindow.SetError(localizer.GetUpdateFailureText(exception.Kind));
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Update delivery failed.");
                updateWindow.SetError(localizer.GetUpdateText("UpdateUnavailable"));
            }
        };

        updateWindow.ShowDialog();
    }

    private void OpenRepositoryPage(Window owner)
    {
        OpenExternalUri(App.Host.ApplicationMetadata.RepositoryUri, owner);
    }

    private void OpenExternalUri(Uri uri, Window owner)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open external URI {Uri}.", uri);
            MessageBox.Show(
                owner,
                localizer.GetUpdateText("UpdateLaunchFailedError"),
                localizer.GetUpdateText("SettingsHelpHeader"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RecordManualUpdateCheckAsync(UpdateCheckResult result)
    {
        try
        {
            var store = new JsonUpdateStateStore(
                App.Host.Paths,
                App.Host.LoggerFactory.CreateLogger<JsonUpdateStateStore>());
            var state = await store.LoadAsync().ConfigureAwait(true);
            await store.SaveAsync(state with
            {
                LastCheckedUtc = DateTimeOffset.UtcNow,
                LastNotifiedVersion = result.Update?.Version.ToString() ?? state.LastNotifiedVersion,
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Manual update state could not be persisted.");
        }
    }
}
