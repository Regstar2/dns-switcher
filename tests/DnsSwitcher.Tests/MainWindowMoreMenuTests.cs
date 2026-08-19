namespace DnsSwitcher.Tests;

public sealed class MainWindowMoreMenuTests
{
    [Fact]
    public void MoreMenu_ExposesHealthAboutAndHelpRoutes()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "MainWindow.Enhancements.cs"));

        Assert.Contains("MoreHealthMenu", source, StringComparison.Ordinal);
        Assert.Contains("MoreAboutMenu", source, StringComparison.Ordinal);
        Assert.Contains("MoreHelpMenu", source, StringComparison.Ordinal);
        Assert.Contains("OpenHealthSettingsAsync(this)", source, StringComparison.Ordinal);
        Assert.Contains("new AboutWindow(localizer, App.Host.ApplicationMetadata)", source, StringComparison.Ordinal);
        Assert.Contains("new HelpWindow(localizer)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutWindow_UsesCanonicalApplicationMetadataVersion()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "AboutWindow.xaml.cs"));

        Assert.Contains("metadata.DisplayVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("1.5.0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpWindow_CoversPrimaryApplicationFunctions()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Ui", "HelpWindow.xaml.cs"));
        var expectedSections = new[]
        {
            "HelpProfilesTitle",
            "HelpAdapterTitle",
            "HelpChecksTitle",
            "HelpHealthTitle",
            "HelpSplitDnsTitle",
            "HelpAgentTitle",
            "HelpTrayTitle",
            "HelpImportExportTitle",
            "HelpSettingsTitle",
            "HelpUpdatesTitle",
            "HelpFilesTitle",
        };

        foreach (var section in expectedSections)
        {
            Assert.Contains(section, source, StringComparison.Ordinal);
        }
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
