using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public static class AgentDeploymentLayout
{
    public const string AgentExecutableName = "DnsSwitcher.Agent.Windows.exe";

    public static string GetDeploymentDirectory(string baseDirectory)
    {
        var applicationRoot = GetApplicationRoot(baseDirectory);

        if (Directory.Exists(Path.Combine(applicationRoot, ".git")))
        {
            return Path.Combine(applicationRoot, "artifacts", "agent-service");
        }

        return Path.Combine(applicationRoot, "service", "agent");
    }

    public static string GetApplicationRoot(string baseDirectory)
    {
        return PortableRootResolver.ResolvePortableRoot(baseDirectory);
    }

    public static string GetDeploymentExecutablePath(string baseDirectory)
    {
        return Path.Combine(GetDeploymentDirectory(baseDirectory), AgentExecutableName);
    }
}
