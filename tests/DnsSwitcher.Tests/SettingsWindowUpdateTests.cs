namespace DnsSwitcher.Tests;

public sealed class SettingsWindowUpdateTests
{
    [Fact]
    public void SettingsWindow_ContainsAboutHelpAndUpdateControls()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "SettingsWindow.xaml"));

        Assert.Contains("x:Name=\"UpdatesHeaderTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AutomaticUpdateChecksCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CheckForUpdatesButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutHeaderTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutVersionTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HelpHeaderTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenGitHubButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_DoesNotHardcodeReleaseVersion()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "SettingsWindow.xaml"));
        var codeBehind = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "SettingsWindow.xaml.cs"));

        Assert.DoesNotContain("Version 1.5.0", xaml, StringComparison.Ordinal);
        Assert.Contains("applicationVersion", codeBehind, StringComparison.Ordinal);
        Assert.Contains("AboutVersionFormat", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopSettings_SavesAutomaticUpdatePreferenceOnlyAfterConfirmation()
    {
        var enhancements = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "MainWindow.Enhancements.cs"));
        var showDialogIndex = enhancements.IndexOf("settingsWindow.ShowDialog() != true", StringComparison.Ordinal);
        var preferenceIndex = enhancements.IndexOf("AutomaticUpdateChecksEnabled = settingsWindow.AutomaticUpdateChecksEnabled", StringComparison.Ordinal);

        Assert.True(showDialogIndex >= 0);
        Assert.True(preferenceIndex > showDialogIndex);
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Repository file was not found: {Path.Combine(pathSegments)}");
    }
}
