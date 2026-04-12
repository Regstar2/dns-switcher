using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Ui.UiModels;

internal sealed record LanguageOption
{
    public required AppLanguage Language { get; init; }

    public required string DisplayName { get; init; }
}
