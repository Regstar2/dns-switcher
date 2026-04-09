using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Ui.UiModels;

internal sealed record ProfileListItem
{
    public required DnsProfile Profile { get; init; }

    public required string StatusText { get; init; }

    public required string SummaryText { get; init; }

    public string Id => Profile.Id;

    public string Name => Profile.Name;

    public string ModeText => Profile.Mode.ToString();
}
