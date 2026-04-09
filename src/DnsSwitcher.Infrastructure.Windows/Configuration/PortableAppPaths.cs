using DnsSwitcher.Core.Abstractions;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class PortableAppPaths : IAppPaths
{
    private const string SolutionFileName = "DnsSwitcher.sln";
    public const string DataDirectoryName = "data";
    public const string ConfigDirectoryName = "config";
    public const string LogsDirectoryName = "logs";
    public const string ProfilesFileName = "profiles.json";
    public const string LogFileName = "dns-switcher.log";
    private readonly string? migrationSourceAppDirectory;

    public PortableAppPaths(string appDirectory)
        : this(appDirectory, Path.Combine(Path.GetFullPath(appDirectory), ConfigDirectoryName, ProfilesFileName))
    {
    }

    public PortableAppPaths(string appDirectory, string profilesFilePath)
        : this(appDirectory, profilesFilePath, migrationSourceAppDirectory: null)
    {
    }

    private PortableAppPaths(string appDirectory, string profilesFilePath, string? migrationSourceAppDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(profilesFilePath);

        AppDirectory = Path.GetFullPath(appDirectory);
        ProfilesFilePath = Path.GetFullPath(profilesFilePath);
        ConfigDirectory = Path.GetDirectoryName(ProfilesFilePath)
            ?? throw new InvalidOperationException("Config directory could not be determined.");
        LogDirectory = Path.Combine(AppDirectory, LogsDirectoryName);
        LogFilePath = Path.Combine(LogDirectory, LogFileName);
        this.migrationSourceAppDirectory = string.IsNullOrWhiteSpace(migrationSourceAppDirectory)
            ? null
            : Path.GetFullPath(migrationSourceAppDirectory);
    }

    public string AppDirectory { get; }

    public string ConfigDirectory { get; }

    public string ProfilesFilePath { get; }

    public string LogDirectory { get; }

    public string LogFilePath { get; }

    public static PortableAppPaths CreateDefault(string? baseDirectoryOverride = null)
    {
        var baseDirectory = Path.GetFullPath(baseDirectoryOverride ?? AppContext.BaseDirectory);
        var localDataDirectory = Path.Combine(baseDirectory, DataDirectoryName);
        var solutionRoot = FindSolutionRoot(baseDirectory);

        if (solutionRoot is null)
        {
            return new PortableAppPaths(localDataDirectory);
        }

        var sharedDataDirectory = Path.Combine(solutionRoot, DataDirectoryName);
        return new PortableAppPaths(
            sharedDataDirectory,
            Path.Combine(sharedDataDirectory, ConfigDirectoryName, ProfilesFileName),
            localDataDirectory);
    }

    public static PortableAppPaths CreateFromConfigPath(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var fullPath = Path.GetFullPath(configPath);

        if (Directory.Exists(fullPath) || !Path.HasExtension(fullPath))
        {
            return new PortableAppPaths(fullPath);
        }

        var configDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Config directory could not be determined.");

        return new PortableAppPaths(configDirectory, fullPath);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
        MigrateLegacyConfigIfNeeded();
    }

    private void MigrateLegacyConfigIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(migrationSourceAppDirectory))
        {
            return;
        }

        if (string.Equals(
                Path.GetFullPath(migrationSourceAppDirectory),
                AppDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceConfigDirectory = Path.Combine(migrationSourceAppDirectory, ConfigDirectoryName);

        if (!Directory.Exists(sourceConfigDirectory))
        {
            return;
        }

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceConfigDirectory))
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var targetFilePath = Path.Combine(ConfigDirectory, fileName);

            if (File.Exists(targetFilePath))
            {
                continue;
            }

            File.Copy(sourceFilePath, targetFilePath, overwrite: false);
        }
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
}
