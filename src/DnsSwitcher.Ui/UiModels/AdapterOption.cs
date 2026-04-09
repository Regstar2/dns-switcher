using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Ui.UiModels;

internal sealed record AdapterOption
{
    public required string DisplayName { get; init; }

    public string? SelectionValue { get; init; }

    public NetworkAdapter? Adapter { get; init; }

    public bool IsAutomatic => SelectionValue is null;
}
