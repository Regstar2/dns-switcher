using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class AppLocalizerTests
{
    [Fact]
    public void Get_ReturnsRussianText_WhenConfiguredLanguageIsRussian()
    {
        var localizer = new AppLocalizer(AppLanguage.Russian);

        Assert.Equal("Профили", localizer["ProfilesHeader"]);
    }

    [Fact]
    public void Get_ReturnsEnglishText_WhenConfiguredLanguageIsEnglish()
    {
        var localizer = new AppLocalizer(AppLanguage.English);

        Assert.Equal("Profiles", localizer["ProfilesHeader"]);
    }
}
