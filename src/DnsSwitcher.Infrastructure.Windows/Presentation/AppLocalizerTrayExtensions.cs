using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class AppLocalizerTrayExtensions
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsSystemTrayHeader"] = "System tray",
        ["SettingsTrayDnsActionsTitle"] = "DNS actions",
        ["SettingsTrayDnsActionsDescription"] = "Show quick DNS enable, disable, and profile switching commands.",
        ["SettingsTrayDiagnosticsTitle"] = "Diagnostics",
        ["SettingsTrayDiagnosticsDescription"] = "Show DNS tests, site tests, benchmark, and health quick actions.",
        ["SettingsTrayProfilesTitle"] = "Profiles",
        ["SettingsTrayProfilesDescription"] = "Show quick profile selection in the tray.",
        ["SettingsTraySplitDnsTitle"] = "Split DNS",
        ["SettingsTraySplitDnsDescription"] = "Show Split DNS controls in the tray.",
        ["SettingsTrayAgentTitle"] = "Agent",
        ["SettingsTrayAgentDescription"] = "Show Agent service controls in the tray.",
        ["SettingsTrayAdapterNameTitle"] = "Adapter name",
        ["SettingsTrayAdapterNameDescription"] = "Show the selected network adapter in the tray.",
        ["SettingsTrayNotificationsTitle"] = "Notifications",
        ["SettingsTrayNotificationsDescription"] = "Show tray notifications for completed actions and diagnostics.",
    };

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsSystemTrayHeader"] = "Системный трей",
        ["SettingsTrayDnsActionsTitle"] = "Действия DNS",
        ["SettingsTrayDnsActionsDescription"] = "Показывать быстрые команды включения, отключения и переключения DNS-профиля.",
        ["SettingsTrayDiagnosticsTitle"] = "Диагностика",
        ["SettingsTrayDiagnosticsDescription"] = "Показывать тест DNS, тест сайтов, бенчмарк и быстрые действия DNS Health.",
        ["SettingsTrayProfilesTitle"] = "Профили",
        ["SettingsTrayProfilesDescription"] = "Показывать быстрый выбор DNS-профиля в трее.",
        ["SettingsTraySplitDnsTitle"] = "Split DNS",
        ["SettingsTraySplitDnsDescription"] = "Показывать элементы управления Split DNS в трее.",
        ["SettingsTrayAgentTitle"] = "Агент",
        ["SettingsTrayAgentDescription"] = "Показывать элементы управления службой агента в трее.",
        ["SettingsTrayAdapterNameTitle"] = "Имя адаптера",
        ["SettingsTrayAdapterNameDescription"] = "Показывать выбранный сетевой адаптер в трее.",
        ["SettingsTrayNotificationsTitle"] = "Уведомления",
        ["SettingsTrayNotificationsDescription"] = "Показывать уведомления трея после операций и диагностических проверок.",
    };

    public static string GetTraySettingsText(this AppLocalizer localizer, string key)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        var catalog = localizer.EffectiveLanguage == AppLanguage.Russian ? Russian : English;
        return catalog.TryGetValue(key, out var value) ? value : localizer[key];
    }
}
