using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Tray;

namespace DnsSwitcher.Tests;

public sealed class TrayMenuVisibilityPolicyTests
{
    public static TheoryData<TraySettings, TrayMenuEntryKind[]> LayoutCases => new()
    {
        { TraySettings.Default, [TrayMenuEntryKind.Adapter, TrayMenuEntryKind.DnsActions, TrayMenuEntryKind.Diagnostics, TrayMenuEntryKind.SplitDns, TrayMenuEntryKind.Agent, TrayMenuEntryKind.Profiles] },
        { new TraySettings { ShowDnsActions = false, ShowDiagnostics = false, ShowSplitDns = false, ShowAgent = false, ShowProfiles = false }, [TrayMenuEntryKind.Adapter] },
        { new TraySettings { ShowDnsActions = false, ShowDiagnostics = false, ShowSplitDns = false, ShowAgent = false }, [TrayMenuEntryKind.Adapter, TrayMenuEntryKind.Profiles] },
        { new TraySettings { ShowDnsActions = false, ShowSplitDns = false, ShowAgent = false, ShowProfiles = false }, [TrayMenuEntryKind.Adapter, TrayMenuEntryKind.Diagnostics] },
        { new TraySettings { ShowDnsActions = false, ShowDiagnostics = false, ShowProfiles = false }, [TrayMenuEntryKind.Adapter, TrayMenuEntryKind.SplitDns, TrayMenuEntryKind.Agent] },
        { new TraySettings { ShowAdapterName = false }, [TrayMenuEntryKind.DnsActions, TrayMenuEntryKind.Diagnostics, TrayMenuEntryKind.SplitDns, TrayMenuEntryKind.Agent, TrayMenuEntryKind.Profiles] },
        { new TraySettings { ShowDiagnostics = false }, [TrayMenuEntryKind.Adapter, TrayMenuEntryKind.DnsActions, TrayMenuEntryKind.SplitDns, TrayMenuEntryKind.Agent, TrayMenuEntryKind.Profiles] },
    };

    [Theory]
    [MemberData(nameof(LayoutCases))]
    public void BuildLayout_ContainsExpectedVisibleOptionalEntries_AndValidSeparators(
        TraySettings settings,
        TrayMenuEntryKind[] expectedOptionalEntries)
    {
        var layout = TrayMenuVisibilityPolicy.BuildLayout(settings);

        Assert.Contains(TrayMenuEntryKind.OpenUi, layout);
        Assert.Contains(TrayMenuEntryKind.Status, layout);
        Assert.Contains(TrayMenuEntryKind.Settings, layout);
        Assert.Contains(TrayMenuEntryKind.Exit, layout);

        var optionalEntries = layout
            .Where(item => item is TrayMenuEntryKind.Adapter
                or TrayMenuEntryKind.DnsActions
                or TrayMenuEntryKind.Diagnostics
                or TrayMenuEntryKind.SplitDns
                or TrayMenuEntryKind.Agent
                or TrayMenuEntryKind.Profiles)
            .ToArray();
        Assert.Equal(expectedOptionalEntries, optionalEntries);

        Assert.NotEqual(TrayMenuEntryKind.Separator, layout[0]);
        Assert.NotEqual(TrayMenuEntryKind.Separator, layout[^1]);
        Assert.DoesNotContain(
            layout.Zip(layout.Skip(1)),
            pair => pair.First == TrayMenuEntryKind.Separator && pair.Second == TrayMenuEntryKind.Separator);
    }
}
