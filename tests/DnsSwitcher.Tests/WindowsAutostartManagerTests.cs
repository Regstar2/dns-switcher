using DnsSwitcher.Infrastructure.Windows.Startup;

namespace DnsSwitcher.Tests;

public sealed class WindowsAutostartManagerTests
{
    [Fact]
    public void BuildCommandLine_QuotesExecutablePathWithSpaces()
    {
        var commandLine = WindowsAutostartManager.BuildCommandLine(
            @"C:\Program Files\DnsSwitcher\DnsSwitcher.Ui.exe",
            "--start-minimized-to-tray");

        Assert.Equal(
            "\"C:\\Program Files\\DnsSwitcher\\DnsSwitcher.Ui.exe\" --start-minimized-to-tray",
            commandLine);
    }

    [Fact]
    public void BuildCommandLine_QuotesArgumentsWithSpaces()
    {
        var commandLine = WindowsAutostartManager.BuildCommandLine(
            @"C:\Apps\DnsSwitcher\DnsSwitcher.Ui.exe",
            "--label",
            "Work Profile");

        Assert.Equal(
            "C:\\Apps\\DnsSwitcher\\DnsSwitcher.Ui.exe --label \"Work Profile\"",
            commandLine);
    }
}
