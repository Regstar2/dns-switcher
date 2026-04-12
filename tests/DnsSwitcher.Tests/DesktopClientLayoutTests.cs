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
}
