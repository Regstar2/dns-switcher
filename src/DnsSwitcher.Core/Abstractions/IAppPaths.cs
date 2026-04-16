namespace DnsSwitcher.Core.Abstractions;

public interface IAppPaths
{
    string AppDirectory { get; }

    string ConfigDirectory { get; }

    string ProfilesFilePath { get; }

    string DnsBenchmarkHistoryFilePath { get; }

    string DnsHealthSettingsFilePath { get; }

    string DnsHealthStateFilePath { get; }

    string SplitDnsRulesFilePath { get; }

    string LogDirectory { get; }

    string LogFilePath { get; }
}
