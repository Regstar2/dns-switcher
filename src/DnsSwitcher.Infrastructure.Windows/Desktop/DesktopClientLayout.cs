namespace DnsSwitcher.Infrastructure.Windows.Desktop;

public static class DesktopClientLayout
{
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

    public static string? TryGetTrayExecutablePath(string baseDirectory)
    {
        return TryGetExecutablePath(baseDirectory, "DnsSwitcher.Tray", "tray");
    }

    public static string? TryGetUiExecutablePath(string baseDirectory)
    {
        return TryGetExecutablePath(baseDirectory, "DnsSwitcher.Ui", "ui");
    }

    private static string? TryGetExecutablePath(string baseDirectory, string projectName, string publishedFolderName)
    {
        var normalizedBaseDirectory = Path.GetFullPath(baseDirectory);
        var applicationRoot = GetApplicationRoot(normalizedBaseDirectory);
        var configuration = TryGetBuildConfiguration(normalizedBaseDirectory);
        var siblingProjectOutputPath = TryGetSiblingProjectOutputPath(applicationRoot, projectName, configuration);

        var candidates = new[]
        {
            Path.Combine(normalizedBaseDirectory, $"{projectName}.exe"),
            siblingProjectOutputPath,
            Path.Combine(applicationRoot, $"{projectName}.exe"),
            Path.Combine(applicationRoot, publishedFolderName, $"{projectName}.exe"),
            Path.Combine(applicationRoot, "artifacts", "release", "v1.3.0", publishedFolderName, $"{projectName}.exe"),
            Path.Combine(applicationRoot, "artifacts", "release", "v1.0", publishedFolderName, $"{projectName}.exe"),
        };

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Path.GetFullPath(candidate!))
            .FirstOrDefault(File.Exists);
    }

    private static string? TryGetBuildConfiguration(string baseDirectory)
    {
        var directory = new DirectoryInfo(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (!directory.Name.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var configurationDirectory = directory.Parent;
        var binDirectory = configurationDirectory?.Parent;

        return binDirectory?.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) == true
            ? configurationDirectory?.Name
            : null;
    }

    private static string? TryGetSiblingProjectOutputPath(string applicationRoot, string projectName, string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        return Path.Combine(
            applicationRoot,
            "src",
            projectName,
            "bin",
            configuration,
            "net10.0-windows",
            $"{projectName}.exe");
    }

    private static bool IsPublishedClientDirectory(DirectoryInfo directory)
    {
        return directory.Name.Equals("agent", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("cli", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("tray", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Equals("ui", StringComparison.OrdinalIgnoreCase);
    }
}
