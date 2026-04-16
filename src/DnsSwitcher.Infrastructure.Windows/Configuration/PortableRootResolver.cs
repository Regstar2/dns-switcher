namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public static class PortableRootResolver
{
    private const string SolutionFileName = "DnsSwitcher.sln";

    public static string ResolvePortableRoot(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var normalizedBaseDirectory = Path.GetFullPath(baseDirectory);
        var directory = new DirectoryInfo(normalizedBaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));

        if (IsServiceAgentDirectory(directory) && directory.Parent?.Parent is not null)
        {
            return directory.Parent.Parent.FullName;
        }

        if (IsPublishedClientDirectory(directory) && directory.Parent is not null)
        {
            return directory.Parent.FullName;
        }

        var solutionRoot = FindSolutionRoot(normalizedBaseDirectory);

        if (solutionRoot is not null)
        {
            return solutionRoot;
        }

        return normalizedBaseDirectory;
    }

    public static string ResolveLegacyLocalDataDirectory(string baseDirectory)
    {
        return Path.Combine(Path.GetFullPath(baseDirectory), PortableAppPaths.DataDirectoryName);
    }

    private static string? FindSolutionRoot(string baseDirectory)
    {
        var directory = new DirectoryInfo(baseDirectory);

        while (directory is not null)
        {
            var candidatePath = Path.Combine(directory.FullName, SolutionFileName);

            if (File.Exists(candidatePath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsServiceAgentDirectory(DirectoryInfo directory)
    {
        return directory.Name.Equals("agent", StringComparison.OrdinalIgnoreCase)
            && directory.Parent?.Name.Equals("service", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsPublishedClientDirectory(DirectoryInfo directory)
    {
        return directory.Name.Equals("agent", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("cli", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("tray", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("ui", StringComparison.OrdinalIgnoreCase);
    }
}
