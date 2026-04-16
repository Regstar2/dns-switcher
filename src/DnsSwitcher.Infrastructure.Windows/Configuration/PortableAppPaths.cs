using DnsSwitcher.Core.Abstractions;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class PortableAppPaths : IAppPaths
{
    public const string DataDirectoryName = "data";
    public const string ConfigDirectoryName = "config";
    public const string LogsDirectoryName = "logs";
    public const string ProfilesFileName = "profiles.json";
    public const string DnsBenchmarkHistoryFileName = "dns-benchmark-history.json";
    public const string DnsHealthSettingsFileName = "dns-health-settings.json";
    public const string DnsHealthStateFileName = "dns-health-state.json";
    public const string SplitDnsRulesFileName = "split-dns-rules.json";
    public const string LogFileName = "dns-switcher.log";
    private readonly string? migrationSourceAppDirectory;

    public PortableAppPaths(string appDirectory)
        : this(
            appDirectory,
            Path.Combine(Path.GetFullPath(appDirectory), ConfigDirectoryName, ProfilesFileName),
            migrationSourceAppDirectory: null)
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
        DnsBenchmarkHistoryFilePath = Path.Combine(ConfigDirectory, DnsBenchmarkHistoryFileName);
        DnsHealthSettingsFilePath = Path.Combine(ConfigDirectory, DnsHealthSettingsFileName);
        DnsHealthStateFilePath = Path.Combine(ConfigDirectory, DnsHealthStateFileName);
        SplitDnsRulesFilePath = Path.Combine(ConfigDirectory, SplitDnsRulesFileName);
        LogDirectory = Path.Combine(AppDirectory, LogsDirectoryName);
        LogFilePath = Path.Combine(LogDirectory, LogFileName);
        this.migrationSourceAppDirectory = string.IsNullOrWhiteSpace(migrationSourceAppDirectory)
            ? null
            : Path.GetFullPath(migrationSourceAppDirectory);
    }

    public string AppDirectory { get; }

    public string ConfigDirectory { get; }

    public string ProfilesFilePath { get; }

    public string DnsBenchmarkHistoryFilePath { get; }

    public string DnsHealthSettingsFilePath { get; }

    public string DnsHealthStateFilePath { get; }

    public string SplitDnsRulesFilePath { get; }

    public string LogDirectory { get; }

    public string LogFilePath { get; }

    public static PortableAppPaths CreateDefault(string? baseDirectoryOverride = null)
    {
        var baseDirectory = Path.GetFullPath(baseDirectoryOverride ?? AppContext.BaseDirectory);
        var portableRoot = PortableRootResolver.ResolvePortableRoot(baseDirectory);
        var sharedDataDirectory = Path.Combine(portableRoot, DataDirectoryName);
        var localDataDirectory = PortableRootResolver.ResolveLegacyLocalDataDirectory(baseDirectory);

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
}
