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
        ["CloseButton"] = "Close",
        ["MoreHealthMenu"] = "Health",
        ["MoreAboutMenu"] = "About",
        ["MoreHelpMenu"] = "Help",
        ["AboutWindowTitle"] = "About DnsSwitcher",
        ["AboutDetailedSummary"] = "DnsSwitcher is a Windows utility for managing DNS profiles and related DNS behavior from one desktop interface and tray menu.",
        ["AboutCapabilitiesHeader"] = "What the application can do",
        ["AboutCapabilitiesBody"] = "Create and switch IPv4/IPv6 DNS profiles, restore automatic DHCP DNS, run DNS and site diagnostics, benchmark resolvers, monitor DNS health with controlled failover, and apply domain-specific Split DNS rules through Windows NRPT. The tray provides the same common actions without keeping the main window open.",
        ["AboutArchitectureHeader"] = "How it works",
        ["AboutArchitectureBody"] = "Normal user interaction stays in the desktop and tray applications. Operations that require elevated Windows networking privileges can be delegated to the DnsSwitcher Agent service. Profiles and application configuration are stored locally. Update packages are accepted only from the configured release source and the installer must pass SHA-256 verification before launch.",
        ["AboutLicenseHeader"] = "Project and license",
        ["AboutLicenseBody"] = "DnsSwitcher is distributed under the MIT License. Source code, documentation, release history, and issue tracking are available in the project repository on GitHub.",
        ["HelpWindowTitle"] = "DnsSwitcher Help",
        ["HelpWindowHeader"] = "Using DnsSwitcher",
        ["HelpWindowIntro"] = "This guide explains the main functions, when to use them, and the safety considerations that matter when changing DNS settings.",
        ["HelpProfilesTitle"] = "DNS profiles",
        ["HelpProfilesBody"] = "Profiles store reusable DNS configurations. Create a profile for each resolver or environment you use, select it, and choose Apply to configure the selected network adapter. Use Reset when you want Windows to receive DNS automatically from DHCP again. Static profiles are useful for public resolvers, filtering DNS, lab networks, or known corporate resolvers.",
        ["HelpAdapterTitle"] = "Network adapter selection",
        ["HelpAdapterBody"] = "Choose Automatic when DnsSwitcher should work with the current default adapter. Select a specific adapter when a computer has several active interfaces, such as Ethernet, Wi-Fi, VPN, or virtual adapters, and the DNS change must target only one of them. Check the selected adapter before applying or resetting DNS.",
        ["HelpChecksTitle"] = "Checks and diagnostics",
        ["HelpChecksBody"] = "Test DNS checks whether the current resolver can answer DNS queries. Test Sites checks practical reachability for configured test sites. Benchmark compares resolver response performance. Health Check performs one Health Monitor evaluation immediately. Use these tools to distinguish a DNS problem from a general network or site-specific problem before changing profiles.",
        ["HelpHealthTitle"] = "DNS Health and failover",
        ["HelpHealthBody"] = "Health Monitor periodically checks DNS operation. Use Notify only when you want detection without automatic changes. Use fallback or a failover chain when DNS availability is more important than staying on one resolver. Failure and recovery thresholds prevent a single transient error from causing a switch; cooldown limits repeated switching. Configure and test fallback profiles before enabling automatic failover.",
        ["HelpSplitDnsTitle"] = "Split DNS",
        ["HelpSplitDnsBody"] = "Split DNS sends selected domain namespaces to a specific DNS profile while other names continue through the normal system resolver. Use it for corporate, VPN, laboratory, or private namespaces that require a dedicated DNS server. DnsSwitcher implements this through Windows NRPT. Apply writes the managed rules; Reset removes DnsSwitcher-managed rules. Browser Secure DNS/DoH or applications with their own resolver can bypass Windows DNS policy.",
        ["HelpAgentTitle"] = "DnsSwitcher Agent",
        ["HelpAgentBody"] = "The Agent is a Windows service used for networking operations that require elevated privileges, including privileged DNS and Split DNS actions. Keeping it installed and running avoids repeated elevation prompts for supported operations. If an action fails unexpectedly, check Agent status before reinstalling it or changing network settings manually.",
        ["HelpTrayTitle"] = "System tray",
        ["HelpTrayBody"] = "The tray provides quick access to DNS switching, checks, Health, Split DNS, Agent actions, profiles, and Settings. Use Settings → System tray to hide groups you do not need. Hiding a menu group changes only its visibility; it does not disable the underlying feature or reset its current state.",
        ["HelpImportExportTitle"] = "Import and export",
        ["HelpImportExportBody"] = "Import loads DNS profiles from JSON. Export saves the selected profile, and Export all profiles creates a backup or migration file containing all profiles. Use export before major profile edits or when moving the configuration to another machine. Review imported DNS addresses before applying them.",
        ["HelpSettingsTitle"] = "Settings",
        ["HelpSettingsBody"] = "Settings controls language, theme, startup behavior, minimize-to-tray behavior, tray menu visibility, Health/Split DNS management shortcuts, and update checking. Changes are saved only when the Settings dialog is confirmed; Cancel leaves the stored configuration unchanged.",
        ["HelpUpdatesTitle"] = "Updates",
        ["HelpUpdatesBody"] = "Check for updates performs an on-demand release check. Automatic checks can be enabled or disabled in Settings and run in the background with throttling. Installing an update is always a user-confirmed action. When direct update delivery is available, DnsSwitcher downloads only the expected Windows x64 installer and verifies its published SHA-256 checksum before launching setup.",
        ["HelpFilesTitle"] = "Config and logs",
        ["HelpFilesBody"] = "Open Config shows the local configuration directory used by DnsSwitcher. Open Logs shows diagnostic logs that are useful when reporting a failure. Avoid editing configuration files while DnsSwitcher is actively writing settings, and review logs for private hostnames, DNS addresses, or machine-specific information before sharing them.",
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
        ["CloseButton"] = "Закрыть",
        ["MoreHealthMenu"] = "Health",
        ["MoreAboutMenu"] = "О приложении",
        ["MoreHelpMenu"] = "Помощь",
        ["AboutWindowTitle"] = "О DnsSwitcher",
        ["AboutDetailedSummary"] = "DnsSwitcher — утилита для Windows, которая объединяет управление DNS-профилями и связанными DNS-функциями в одном настольном интерфейсе и меню системного трея.",
        ["AboutCapabilitiesHeader"] = "Возможности приложения",
        ["AboutCapabilitiesBody"] = "Создание и переключение DNS-профилей IPv4/IPv6, возврат к автоматическому DNS через DHCP, диагностика DNS и доступности сайтов, сравнение скорости резолверов, контроль DNS Health с управляемым failover и доменные правила Split DNS через Windows NRPT. Основные действия также доступны из системного трея без постоянно открытого главного окна.",
        ["AboutArchitectureHeader"] = "Как это работает",
        ["AboutArchitectureBody"] = "Обычное взаимодействие выполняют Desktop UI и Tray. Сетевые операции, которым нужны повышенные права Windows, могут передаваться службе DnsSwitcher Agent. Профили и настройки хранятся локально. Пакет обновления принимается только из настроенного источника релизов, а установщик перед запуском должен пройти проверку SHA-256.",
        ["AboutLicenseHeader"] = "Проект и лицензия",
        ["AboutLicenseBody"] = "DnsSwitcher распространяется по лицензии MIT. Исходный код, документация, история релизов и система отслеживания задач доступны в репозитории проекта на GitHub.",
        ["HelpWindowTitle"] = "Помощь DnsSwitcher",
        ["HelpWindowHeader"] = "Как пользоваться DnsSwitcher",
        ["HelpWindowIntro"] = "Здесь описаны основные функции приложения, ситуации, в которых их стоит применять, и важные меры предосторожности при изменении DNS.",
        ["HelpProfilesTitle"] = "DNS-профили",
        ["HelpProfilesBody"] = "Профиль хранит готовую конфигурацию DNS. Создай отдельный профиль для каждого используемого резолвера или окружения, выбери его и нажми «Применить», чтобы настроить выбранный сетевой адаптер. «Сброс» возвращает автоматическое получение DNS через DHCP. Статические профили подходят для публичных DNS, фильтрующих DNS, лабораторных сетей и известных корпоративных резолверов.",
        ["HelpAdapterTitle"] = "Выбор сетевого адаптера",
        ["HelpAdapterBody"] = "Режим «Автоматически» подходит, когда DnsSwitcher должен работать с текущим основным сетевым адаптером. Конкретный адаптер выбирай, если одновременно используются Ethernet, Wi-Fi, VPN или виртуальные интерфейсы и DNS нужно изменить только на одном из них. Перед применением или сбросом всегда проверяй выбранный адаптер.",
        ["HelpChecksTitle"] = "Проверки и диагностика",
        ["HelpChecksBody"] = "«Тест DNS» проверяет, отвечает ли текущий резолвер на DNS-запросы. «Тест сайтов» проверяет практическую доступность настроенных тестовых сайтов. Benchmark сравнивает скорость ответа DNS. Health Check немедленно выполняет одну проверку Health Monitor. Эти инструменты помогают отличить проблему DNS от общей проблемы сети или отдельного сайта до смены профиля.",
        ["HelpHealthTitle"] = "DNS Health и failover",
        ["HelpHealthBody"] = "Health Monitor периодически проверяет работу DNS. Режим Notify only используй, если нужно только обнаружение проблемы без автоматической смены DNS. Fallback или цепочка failover подходят, когда доступность DNS важнее сохранения одного резолвера. Порог ошибок и восстановления защищает от переключения из-за единичного сбоя, а cooldown ограничивает повторные переключения. Перед включением автоматического failover заранее настрой и проверь резервные профили.",
        ["HelpSplitDnsTitle"] = "Split DNS",
        ["HelpSplitDnsBody"] = "Split DNS направляет выбранные доменные пространства имён на отдельный DNS-профиль, а остальные запросы оставляет обычному системному DNS. Это полезно для корпоративных, VPN, лабораторных и приватных доменов, которым нужен специальный DNS-сервер. DnsSwitcher использует Windows NRPT: «Применить» записывает управляемые правила, «Сброс» удаляет правила DnsSwitcher. Secure DNS/DoH в браузере и приложения с собственным резолвером могут обходить системную DNS-политику Windows.",
        ["HelpAgentTitle"] = "DnsSwitcher Agent",
        ["HelpAgentBody"] = "Agent — служба Windows для сетевых операций, которым требуются повышенные права, включая привилегированные DNS-операции и Split DNS. Установленная и работающая служба позволяет выполнять поддерживаемые действия без повторных запросов повышения прав. Если сетевое действие неожиданно не выполняется, сначала проверь статус Agent, а уже затем переустанавливай службу или меняй сеть вручную.",
        ["HelpTrayTitle"] = "Системный трей",
        ["HelpTrayBody"] = "Tray даёт быстрый доступ к переключению DNS, проверкам, Health, Split DNS, Agent, профилям и настройкам. В «Настройки → Системный трей» можно скрыть ненужные группы. Скрытие пункта влияет только на видимость меню: функция не выключается и её текущее состояние не сбрасывается.",
        ["HelpImportExportTitle"] = "Импорт и экспорт",
        ["HelpImportExportBody"] = "Импорт загружает DNS-профили из JSON. Экспорт сохраняет выбранный профиль, а «Экспорт всех профилей» создаёт резервный или переносимый файл со всеми профилями. Делай экспорт перед крупным редактированием профилей или переносом конфигурации на другой компьютер. Перед применением импортированных профилей проверь DNS-адреса.",
        ["HelpSettingsTitle"] = "Настройки",
        ["HelpSettingsBody"] = "В настройках находятся язык, тема, автозапуск, сворачивание в трей, видимость пунктов tray-меню, переходы к Health/Split DNS и проверка обновлений. Изменения сохраняются только после подтверждения окна настроек; «Отмена» не изменяет сохранённую конфигурацию.",
        ["HelpUpdatesTitle"] = "Обновления",
        ["HelpUpdatesBody"] = "«Проверить обновления» выполняет ручную проверку релиза. Автоматическую проверку можно включить или выключить в настройках; она выполняется в фоне с ограничением частоты. Установка всегда требует решения пользователя. Когда прямая доставка обновления доступна, DnsSwitcher скачивает только ожидаемый установщик Windows x64 и проверяет опубликованную SHA-256 сумму перед запуском.",
        ["HelpFilesTitle"] = "Конфигурация и логи",
        ["HelpFilesBody"] = "«Открыть Config» показывает локальный каталог конфигурации DnsSwitcher. «Открыть Logs» показывает диагностические журналы, полезные при разборе ошибок. Не редактируй конфигурационные файлы в момент, когда DnsSwitcher сохраняет настройки, и перед отправкой логов проверь их на приватные домены, DNS-адреса и данные конкретного компьютера.",
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
