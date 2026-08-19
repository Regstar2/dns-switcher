using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class AppLocalizerUpdateTests
{
    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Russian)]
    public void UpdateAndAboutKeys_AreLocalized(AppLanguage language)
    {
        var localizer = new AppLocalizer(language);
        var keys = new[]
        {
            "SettingsUpdatesHeader",
            "SettingsAutomaticUpdateCheckTitle",
            "SettingsAboutHeader",
            "SettingsHelpHeader",
            "AboutVersionFormat",
            "CheckForUpdatesButton",
            "OpenGitHubButton",
            "UpdateAvailableFormat",
            "UpdateInstallButton",
            "UpdateReleaseNotesButton",
            "UpdateLaterButton",
            "UpdateChecksumMismatchError",
        };

        foreach (var key in keys)
        {
            var value = localizer.GetUpdateText(key);
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.NotEqual(key, value);
        }
    }
}
