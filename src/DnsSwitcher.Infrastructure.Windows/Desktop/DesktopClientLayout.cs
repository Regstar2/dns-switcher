using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Desktop;

public static class DesktopClientLayout
{
    public static string GetApplicationRoot(string baseDirectory)
    {
        return PortableRootResolver.ResolvePortableRoot(baseDirectory);
    }

    public static string? TryGetTrayExecutablePath(string baseDirectory)
    {
        return TryGetExecutablePath(baseDirectory, "DnsSwitcher.Tray", "tray");
    }

    public static string? TryGetUiExecutablePath(string baseDirectory)
    {
        return TryGetExecutablePath(baseDirectory, "DnsSwitcher.Ui", "ui");
    }

    public static string? TryGetCliExecutablePath(string baseDirectory)
    {
        return TryGetExecutablePath(baseDirectory, "DnsSwitcher.Cli", "cli", targetFramework: "net10.0");
    }

    private static string? TryGetExecutablePath(
        string baseDirectory,
        string projectName,
        string publishedFolderName,
        string targetFramework = "net10.0-windows")
    {
        var normalizedBaseDirectory = Path.GetFullPath(baseDirectory);
        var applicationRoot = GetApplicationRoot(normalizedBaseDirectory);
        var configuration = TryGetBuildConfiguration(normalizedBaseDirectory);
        var siblingProjectOutputPath = TryGetSiblingProjectOutputPath(applicationRoot, projectName, configuration, targetFramework);

        var candidates = new[]
        {
            Path.Combine(normalizedBaseDirectory, $"{projectName}.exe"),
            siblingProjectOutputPath,
            Path.Combine(applicationRoot, $"{projectName}.exe"),
            Path.Combine(applicationRoot, publishedFolderName, $"{projectName}.exe"),
            Path.Combine(applicationRoot, "artifacts", "release", "v1.4.1", publishedFolderName, $"{projectName}.exe"),
            Path.Combine(applicationRoot, "artifacts", "release", "v1.4.0", publishedFolderName, $"{projectName}.exe"),
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

    private static string? TryGetSiblingProjectOutputPath(
        string applicationRoot,
        string projectName,
        string? configuration,
        string targetFramework)
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
            targetFramework,
            $"{projectName}.exe");
    }
}
