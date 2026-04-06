namespace DnsSwitcher.Core.Abstractions;

public interface IAppPaths
{
    string AppDirectory { get; }

    string ConfigDirectory { get; }

    string ProfilesFilePath { get; }

    string LogDirectory { get; }

    string LogFilePath { get; }
}
