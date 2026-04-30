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

    [Fact]
    public void GetApplicationRoot_ReturnsPackageRoot_ForPublishedCliFolder()
    {
        var baseDirectory = @"C:\Apps\DnsSwitcher\cli\";

        var root = AgentDeploymentLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Apps\DnsSwitcher", root);
    }

    [Fact]
    public void GetApplicationRoot_ReturnsPackageRoot_WhenPublishedPackageIsInsideRepository()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DnsSwitcherTests", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "artifacts", "release", "v1.4.1", "DnsSwitcher-1.4.1-win-x64");
        var cliDirectory = Path.Combine(packageRoot, "cli");

        try
        {
            Directory.CreateDirectory(cliDirectory);
            File.WriteAllText(Path.Combine(tempRoot, "DnsSwitcher.sln"), string.Empty);

            var root = AgentDeploymentLayout.GetApplicationRoot(cliDirectory);

            Assert.Equal(packageRoot, root);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void GetApplicationRoot_ReturnsPackageRoot_ForServiceAgentRuntime()
    {
        var baseDirectory = @"C:\Apps\DnsSwitcher\service\agent\";

        var root = AgentDeploymentLayout.GetApplicationRoot(baseDirectory);

        Assert.Equal(@"C:\Apps\DnsSwitcher", root);
    }
}
