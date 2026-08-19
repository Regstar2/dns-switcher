using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class AppLocalizerUpdateExtensions
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsUpdatesHeader"] = "Updates",
        ["SettingsAutomaticUpdateCheckTitle"] = "Automatically check for updates",
        ["SettingsAutomaticUpdateCheckDescription"] = "Check the official release source in the background. You can turn this off at any time.",
        ["SettingsAboutHeader"] = "About",
        ["SettingsAboutDescription"] = "Windows DNS profile switching, diagnostics, Health Failover and Split DNS.",
        ["SettingsHelpHeader"] = "Help",
        ["SettingsHelpDescription"] = "Documentation, source code, and release information are available on GitHub.",
        ["AboutVersionFormat"] = "Version {0}",
        ["CheckForUpdatesButton"] = "Check for updates",
        ["OpenGitHubButton"] = "Open GitHub",
        ["UpdateDialogTitle"] = "DnsSwitcher update",
        ["UpdateCheckingStatus"] = "Checking for updates...",
        ["UpdateCurrentFormat"] = "DnsSwitcher {0} is up to date.",
        ["UpdateAvailableFormat"] = "DnsSwitcher {0} is available.",
        ["UpdateUnavailable"] = "The update service is currently unavailable.",
        ["UpdateNetworkError"] = "Could not reach the update service. Check the network connection and try again.",
        ["UpdateMissingInstallerError"] = "The release does not contain the expected Windows x64 installer.",
        ["UpdateMissingChecksumError"] = "The release does not contain the required SHA-256 checksum file.",
        ["UpdateInvalidReleaseError"] = "The release metadata is invalid or cannot be trusted.",
        ["UpdateChecksumInvalidError"] = "The published SHA-256 checksum is invalid.",
        ["UpdateChecksumMismatchError"] = "The downloaded installer failed SHA-256 verification and will not be started.",
        ["UpdateLaunchCancelledError"] = "Installer launch was cancelled.",
        ["UpdateLaunchFailedError"] = "The installer could not be started.",
        ["UpdateInstallButton"] = "Download and install",
        ["UpdateReleaseNotesButton"] = "Release notes",
        ["UpdateLaterButton"] = "Later",
        ["UpdateDownloadingStatus"] = "Downloading and verifying the installer...",
        ["UpdateVerifiedStatus"] = "Installer verified. Starting setup...",
        ["UpdateTrayAvailableFormat"] = "DnsSwitcher {0} is available. Open Settings to review and install the update.",
        ["ExportAllProfilesMenu"] = "Export all profiles",
        ["ExportAllProfilesDialogTitle"] = "Export all DNS profiles",
        ["ExportAllProfilesSuccess"] = "All DNS profiles were exported.",
    };

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsUpdatesHeader"] = "Обновления",
        ["SettingsAutomaticUpdateCheckTitle"] = "Автоматически проверять обновления",
        ["SettingsAutomaticUpdateCheckDescription"] = "Фоновая проверка официального источника релизов. Эту опцию можно отключить в любой момент.",
        ["SettingsAboutHeader"] = "О приложении",
        ["SettingsAboutDescription"] = "Переключение DNS-профилей, диагностика, Health Failover и Split DNS для Windows.",
        ["SettingsHelpHeader"] = "Помощь",
        ["SettingsHelpDescription"] = "Документация, исходный код и информация о релизах доступны на GitHub.",
        ["AboutVersionFormat"] = "Версия {0}",
        ["CheckForUpdatesButton"] = "Проверить обновления",
        ["OpenGitHubButton"] = "Открыть GitHub",
        ["UpdateDialogTitle"] = "Обновление DnsSwitcher",
        ["UpdateCheckingStatus"] = "Проверка обновлений...",
        ["UpdateCurrentFormat"] = "Установлена актуальная версия DnsSwitcher {0}.",
        ["UpdateAvailableFormat"] = "Доступна DnsSwitcher {0}.",
        ["UpdateUnavailable"] = "Сервис обновлений сейчас недоступен.",
        ["UpdateNetworkError"] = "Не удалось связаться с сервисом обновлений. Проверь подключение к сети и повтори попытку.",
        ["UpdateMissingInstallerError"] = "В релизе отсутствует ожидаемый установщик Windows x64.",
        ["UpdateMissingChecksumError"] = "В релизе отсутствует обязательный файл SHA-256 контрольных сумм.",
        ["UpdateInvalidReleaseError"] = "Метаданные релиза некорректны или не могут считаться доверенными.",
        ["UpdateChecksumInvalidError"] = "Опубликованная контрольная сумма SHA-256 некорректна.",
        ["UpdateChecksumMismatchError"] = "Загруженный установщик не прошёл проверку SHA-256 и не будет запущен.",
        ["UpdateLaunchCancelledError"] = "Запуск установщика отменён.",
        ["UpdateLaunchFailedError"] = "Не удалось запустить установщик.",
        ["UpdateInstallButton"] = "Скачать и установить",
        ["UpdateReleaseNotesButton"] = "Описание релиза",
        ["UpdateLaterButton"] = "Позже",
        ["UpdateDownloadingStatus"] = "Загрузка и проверка установщика...",
        ["UpdateVerifiedStatus"] = "Установщик проверен. Запуск установки...",
        ["UpdateTrayAvailableFormat"] = "Доступна DnsSwitcher {0}. Открой Настройки, чтобы посмотреть и установить обновление.",
        ["ExportAllProfilesMenu"] = "Экспорт всех профилей",
        ["ExportAllProfilesDialogTitle"] = "Экспорт всех DNS-профилей",
        ["ExportAllProfilesSuccess"] = "Все DNS-профили экспортированы.",
    };

    public static string GetUpdateText(this AppLocalizer localizer, string key)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        if (localizer.EffectiveLanguage == AppLanguage.Russian && Russian.TryGetValue(key, out var russian))
        {
            return russian;
        }

        return English.TryGetValue(key, out var english) ? english : localizer[key];
    }

    public static string FormatUpdateText(this AppLocalizer localizer, string key, params object[] arguments)
    {
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, localizer.GetUpdateText(key), arguments);
    }

    public static string GetUpdateFailureText(this AppLocalizer localizer, UpdateFailureKind? failureKind)
    {
        return failureKind switch
        {
            UpdateFailureKind.Network => localizer.GetUpdateText("UpdateNetworkError"),
            UpdateFailureKind.MissingInstaller => localizer.GetUpdateText("UpdateMissingInstallerError"),
            UpdateFailureKind.MissingChecksum => localizer.GetUpdateText("UpdateMissingChecksumError"),
            UpdateFailureKind.ChecksumInvalid => localizer.GetUpdateText("UpdateChecksumInvalidError"),
            UpdateFailureKind.ChecksumMismatch => localizer.GetUpdateText("UpdateChecksumMismatchError"),
            UpdateFailureKind.LaunchCancelled => localizer.GetUpdateText("UpdateLaunchCancelledError"),
            UpdateFailureKind.LaunchFailed => localizer.GetUpdateText("UpdateLaunchFailedError"),
            UpdateFailureKind.InvalidDownloadUrl or UpdateFailureKind.InvalidRelease => localizer.GetUpdateText("UpdateInvalidReleaseError"),
            _ => localizer.GetUpdateText("UpdateUnavailable"),
        };
    }
}
