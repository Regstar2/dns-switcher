using DnsSwitcher.Infrastructure.Windows.Desktop;

namespace DnsSwitcher.Tests;

public sealed class DesktopClientLayoutTests
{
    [Fact]
    public void GetApplicationRoot_ReturnsRepositoryRoot_ForUiBuildOutput()
    {
        var baseDirectory = @"C:\Base\projects\changeDNS\src\DnsSwitcher.Ui\bin\Release\net10.0-windows\";

        var root = DesktopClientLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Base\projects\changeDNS", root);
    }

    [Fact]
    public void GetApplicationRoot_ReturnsPackageRoot_ForPublishedUiFolder()
    {
        var baseDirectory = @"C:\Apps\DnsSwitcher\ui\";

        var root = DesktopClientLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Apps\DnsSwitcher", root);
    }

    [Fact]
    public void GetApplicationRoot_ReturnsPackageRoot_ForPublishedTrayFolder()
    {
        var baseDirectory = @"C:\Apps\DnsSwitcher\tray\";

        var root = DesktopClientLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Apps\DnsSwitcher", root);
    }
}
