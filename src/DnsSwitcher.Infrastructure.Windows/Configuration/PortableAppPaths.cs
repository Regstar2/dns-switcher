using DnsSwitcher.Core.Abstractions;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class PortableAppPaths : IAppPaths
{
    public const string DataDirectoryName = "data";
    public const string ConfigDirectoryName = "config";
    public const string LogsDirectoryName = "logs";
    public const string ProfilesFileName = "profiles.json";
    public const string LogFileName = "dns-switcher.log";

    public PortableAppPaths(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);

        AppDirectory = Path.GetFullPath(appDirectory);
        ConfigDirectory = Path.Combine(AppDirectory, ConfigDirectoryName);
        ProfilesFilePath = Path.Combine(ConfigDirectory, ProfilesFileName);
        LogDirectory = Path.Combine(AppDirectory, LogsDirectoryName);
        LogFilePath = Path.Combine(LogDirectory, LogFileName);
    }

    public string AppDirectory { get; }

    public string ConfigDirectory { get; }

    public string ProfilesFilePath { get; }

    public string LogDirectory { get; }

    public string LogFilePath { get; }

    public static PortableAppPaths CreateDefault()
    {
        return new PortableAppPaths(Path.Combine(AppContext.BaseDirectory, DataDirectoryName));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
