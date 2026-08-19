using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Tray;

public enum TrayMenuEntryKind
{
    OpenUi,
    Separator,
    Status,
    Adapter,
    DnsActions,
    Diagnostics,
    SplitDns,
    Agent,
    Profiles,
    Settings,
    Exit,
}

public static class TrayMenuVisibilityPolicy
{
    public static bool HasOptionalGroups(TraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.ShowDnsActions
            || settings.ShowDiagnostics
            || settings.ShowSplitDns
            || settings.ShowAgent
            || settings.ShowProfiles;
    }

    public static IReadOnlyList<TrayMenuEntryKind> BuildLayout(TraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var items = new List<TrayMenuEntryKind>
        {
            TrayMenuEntryKind.OpenUi,
            TrayMenuEntryKind.Separator,
            TrayMenuEntryKind.Status,
        };

        if (settings.ShowAdapterName)
        {
            items.Add(TrayMenuEntryKind.Adapter);
        }

        items.Add(TrayMenuEntryKind.Separator);

        if (settings.ShowDnsActions)
        {
            items.Add(TrayMenuEntryKind.DnsActions);
        }

        if (settings.ShowDiagnostics)
        {
            items.Add(TrayMenuEntryKind.Diagnostics);
        }

        if (settings.ShowSplitDns)
        {
            items.Add(TrayMenuEntryKind.SplitDns);
        }

        if (settings.ShowAgent)
        {
            items.Add(TrayMenuEntryKind.Agent);
        }

        if (settings.ShowProfiles)
        {
            items.Add(TrayMenuEntryKind.Profiles);
        }

        if (HasOptionalGroups(settings))
        {
            items.Add(TrayMenuEntryKind.Separator);
        }

        items.Add(TrayMenuEntryKind.Settings);
        items.Add(TrayMenuEntryKind.Separator);
        items.Add(TrayMenuEntryKind.Exit);
        return items;
    }
}
