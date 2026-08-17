using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Ui.UiModels;

internal sealed record ProfileListItem
{
    public required DnsProfile Profile { get; init; }

    public required string StatusText { get; init; }

    public required string SummaryText { get; init; }

    public string Id => Profile.Id;

    public string Name => Profile.Name;

    public string ModeText => Profile.Mode == ProfileMode.Dhcp ? "DHCP" : "STATIC";

    public string DnsSummary => Profile.Mode == ProfileMode.Dhcp
        ? "DHCP"
        : Profile.Ipv4.Count > 0
            ? string.Join(" · ", Profile.Ipv4)
            : Profile.Ipv6.Count > 0
                ? string.Join(" · ", Profile.Ipv6)
                : "—";
}
