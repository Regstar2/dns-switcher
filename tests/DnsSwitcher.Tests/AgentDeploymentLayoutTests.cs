using DnsSwitcher.Infrastructure.Windows.Agent;

namespace DnsSwitcher.Tests;

public sealed class AgentDeploymentLayoutTests
{
    [Fact]
    public void GetApplicationRoot_ReturnsRepositoryRoot_ForCliBuildOutput()
    {
        var baseDirectory = @"C:\Base\projects\changeDNS\src\DnsSwitcher.Cli\bin\Release\net10.0\";

        var root = AgentDeploymentLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Base\projects\changeDNS", root);
    }

    [Fact]
    public void GetApplicationRoot_ReturnsBaseDirectory_ForPublishedLayout()
    {
        var baseDirectory = @"C:\Apps\DnsSwitcher\";

        var root = AgentDeploymentLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Apps\DnsSwitcher\", root);
    }
}
