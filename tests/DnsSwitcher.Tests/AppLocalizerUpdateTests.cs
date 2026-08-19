using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class AppLocalizerUpdateTests
{
    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Russian)]
    public void UpdateAboutHelpAndMoreMenuKeys_AreLocalized(AppLanguage language)
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
            "CloseButton",
            "MoreHealthMenu",
            "MoreAboutMenu",
            "MoreHelpMenu",
            "AboutWindowTitle",
            "AboutDetailedSummary",
            "AboutCapabilitiesHeader",
            "AboutCapabilitiesBody",
            "AboutArchitectureHeader",
            "AboutArchitectureBody",
            "AboutLicenseHeader",
            "AboutLicenseBody",
            "HelpWindowTitle",
            "HelpWindowHeader",
            "HelpWindowIntro",
            "HelpProfilesTitle",
            "HelpProfilesBody",
            "HelpAdapterTitle",
            "HelpAdapterBody",
            "HelpChecksTitle",
            "HelpChecksBody",
            "HelpHealthTitle",
            "HelpHealthBody",
            "HelpSplitDnsTitle",
            "HelpSplitDnsBody",
            "HelpAgentTitle",
            "HelpAgentBody",
            "HelpTrayTitle",
            "HelpTrayBody",
            "HelpImportExportTitle",
            "HelpImportExportBody",
            "HelpSettingsTitle",
            "HelpSettingsBody",
            "HelpUpdatesTitle",
            "HelpUpdatesBody",
            "HelpFilesTitle",
            "HelpFilesBody",
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
