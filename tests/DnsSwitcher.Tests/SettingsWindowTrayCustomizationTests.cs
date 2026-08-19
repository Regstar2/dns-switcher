namespace DnsSwitcher.Tests;

public sealed class SettingsWindowTrayCustomizationTests
{
    [Fact]
    public void SettingsWindow_ContainsScrollableTrayCustomizationControls()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "SettingsWindow.xaml"));

        Assert.Contains("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("CanContentScroll=\"False\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CanContentScroll=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SystemTrayHeaderTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowDnsActionsCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowDiagnosticsCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowProfilesCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowSplitDnsCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowAgentCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShowAdapterNameCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NotificationsEnabledCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsCancel=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_InitializesAndReturnsTraySettingsValues()
    {
        var codeBehind = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "SettingsWindow.xaml.cs"));

        Assert.Contains("TraySettings traySettings", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowDnsActionsCheckBox.IsChecked = traySettings.ShowDnsActions", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowDiagnosticsCheckBox.IsChecked = traySettings.ShowDiagnostics", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowSplitDnsCheckBox.IsChecked = traySettings.ShowSplitDns", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowAgentCheckBox.IsChecked = traySettings.ShowAgent", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowProfilesCheckBox.IsChecked = traySettings.ShowProfiles", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowAdapterNameCheckBox.IsChecked = traySettings.ShowAdapterName", codeBehind, StringComparison.Ordinal);
        Assert.Contains("NotificationsEnabledCheckBox.IsChecked = traySettings.NotificationsEnabled", codeBehind, StringComparison.Ordinal);
        Assert.Contains("public TraySettings EditedTraySettings", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DialogResult = true", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopSettings_PersistsTraySettingsOnlyAfterConfirmedDialog()
    {
        var enhancements = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "MainWindow.Enhancements.cs"));
        var showDialogIndex = enhancements.IndexOf("settingsWindow.ShowDialog() != true", StringComparison.Ordinal);
        var saveIndex = enhancements.IndexOf("traySettingsStore.SaveAsync(settingsWindow.EditedTraySettings)", StringComparison.Ordinal);

        Assert.True(showDialogIndex >= 0);
        Assert.True(saveIndex > showDialogIndex);
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