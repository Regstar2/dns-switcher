using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Ui.UiModels;

public sealed class ThemeOption
{
    public AppTheme Theme { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}
