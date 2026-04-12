namespace DnsSwitcher.Infrastructure.Windows.Agent;

public static class AgentDeploymentLayout
{
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
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var normalizedBaseDirectory = Path.GetFullPath(baseDirectory);
        var directory = new DirectoryInfo(normalizedBaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (directory.Name.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            var configurationDirectory = directory.Parent;
            var binDirectory = configurationDirectory?.Parent;
            var projectDirectory = binDirectory?.Parent;
            var srcDirectory = projectDirectory?.Parent;

            if (binDirectory?.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) == true
                && srcDirectory?.Name.Equals("src", StringComparison.OrdinalIgnoreCase) == true
                && srcDirectory.Parent is not null)
            {
                return srcDirectory.Parent.FullName;
            }
        }

        if (IsPublishedClientDirectory(directory) && directory.Parent is not null)
        {
            return directory.Parent.FullName;
        }

        return normalizedBaseDirectory;
    }

    private static bool IsPublishedClientDirectory(DirectoryInfo directory)
    {
        return directory.Name.Equals("agent", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("cli", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("tray", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("ui", StringComparison.OrdinalIgnoreCase);
    }
}
