using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class AppLocalizerTests
{
    [Fact]
    public void Get_ReturnsRussianText_WhenConfiguredLanguageIsRussian()
    {
        var localizer = new AppLocalizer(AppLanguage.Russian);

        Assert.Equal("Профили", localizer["ProfilesHeader"]);
        Assert.Equal("Агент", localizer["AgentManagerButton"]);
        Assert.Equal("Проверка DNS Health", localizer["HealthCheckTitle"]);
        Assert.Equal("Мониторинг DNS:", localizer["HealthMonitorLabel"]);
    }

    [Fact]
    public void Get_ReturnsEnglishText_WhenConfiguredLanguageIsEnglish()
    {
        var localizer = new AppLocalizer(AppLanguage.English);

        Assert.Equal("Profiles", localizer["ProfilesHeader"]);
        Assert.Equal("Agent", localizer["AgentManagerButton"]);
        Assert.Equal("DNS Health Check", localizer["HealthCheckTitle"]);
    }

    [Fact]
    public void GetTraySettingsText_ReturnsEnglishTraySettingsStrings()
    {
        var localizer = new AppLocalizer(AppLanguage.English);

        Assert.Equal("System tray", localizer.GetTraySettingsText("SettingsSystemTrayHeader"));
        Assert.Equal("DNS actions", localizer.GetTraySettingsText("SettingsTrayDnsActionsTitle"));
        Assert.Contains("benchmark", localizer.GetTraySettingsText("SettingsTrayDiagnosticsDescription"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTraySettingsText_ReturnsRussianTraySettingsStrings()
    {
        var localizer = new AppLocalizer(AppLanguage.Russian);

        Assert.Equal("Системный трей", localizer.GetTraySettingsText("SettingsSystemTrayHeader"));
        Assert.Equal("Действия DNS", localizer.GetTraySettingsText("SettingsTrayDnsActionsTitle"));
        Assert.Contains("Показывать", localizer.GetTraySettingsText("SettingsTrayDiagnosticsDescription"), StringComparison.Ordinal);
    }
}
