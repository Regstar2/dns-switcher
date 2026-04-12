using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Ui.UiModels;

public sealed class ProfileModeOption
{
    public required ProfileMode Mode { get; init; }

    public required string DisplayName { get; init; }
}
