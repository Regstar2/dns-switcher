using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using DnsSwitcher.Infrastructure.Windows.Tray;

namespace DnsSwitcher.Tests;

public sealed class TrayTextFormatterTests
{
    [Fact]
    public void BuildStatusMenuText_UsesProfileNameWithoutAdapter()
    {
        var configuration = CreateConfiguration("Extremely Long DNS Profile Name For Testing Menu Width");
        var status = CreateStatus(matchedProfileId: "test-profile", adapterName: "Very Long Adapter Name");
        var localizer = new AppLocalizer(AppLanguage.English);

        var text = TrayTextFormatter.BuildStatusMenuText(configuration, status, localizer);

        Assert.StartsWith("Status: ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Adapter", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Very Long Adapter Name", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAdapterMenuText_ReturnsNull_WhenAdapterNameIsHidden()
    {
        var status = CreateStatus(matchedProfileId: "test-profile", adapterName: "Wi-Fi Adapter");
        var localizer = new AppLocalizer(AppLanguage.English);

        var text = TrayTextFormatter.BuildAdapterMenuText(status, new TraySettings
        {
            ShowAdapterName = false,
        }, localizer);

        Assert.Null(text);
    }

    [Fact]
    public void BuildNotifyIconText_RespectsNotifyIconLengthLimit()
    {
        var configuration = CreateConfiguration("Profile Name That Is Long Enough To Force Notify Icon Text Truncation");
        var status = CreateStatus(
            matchedProfileId: "test-profile",
            adapterName: "Very Long Adapter Name That Also Needs To Be Trimmed For The Notify Icon");
        var localizer = new AppLocalizer(AppLanguage.English);

        var text = TrayTextFormatter.BuildNotifyIconText(configuration, status, TraySettings.Default, localizer);

        Assert.True(text.Length <= 63);
        Assert.StartsWith("DnsSwitcher", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnableMenuText_TrimsLongProfileName()
    {
        var profile = new DnsProfile
        {
            Id = "test-profile",
            Name = "Profile Name That Is Too Long For The Enable Menu Item",
            Mode = ProfileMode.Static,
            Ipv4 = ["1.1.1.1"],
        };
        var localizer = new AppLocalizer(AppLanguage.English);

        var text = TrayTextFormatter.BuildEnableMenuText(profile, localizer);

        Assert.StartsWith("Enable DNS (", text, StringComparison.Ordinal);
        Assert.Contains("...", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProfileMenuText_KeepsSelectionMarkerWhileTrimming()
    {
        var profile = new DnsProfile
        {
            Id = "test-profile",
            Name = "Profile Name That Is Too Long For The Profiles Submenu",
            Mode = ProfileMode.Static,
            Ipv4 = ["1.1.1.1"],
        };
        var localizer = new AppLocalizer(AppLanguage.English);

        var text = TrayTextFormatter.BuildProfileMenuText(profile, isCurrent: true, isPreferred: false, localizer);

        Assert.EndsWith("[active]", text, StringComparison.Ordinal);
        Assert.Contains("...", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOverviewDetails_LocalizesRussianTrayStatusDetails()
    {
        var configuration = CreateConfiguration("Cloudflare");
        var status = CreateStatus(matchedProfileId: "test-profile", adapterName: "Wi-Fi");
        var localizer = new AppLocalizer(AppLanguage.Russian);
        var splitDnsConfiguration = new SplitDnsConfiguration
        {
            Enabled = true,
            Rules =
            [
                new SplitDnsRule
                {
                    Id = "corp",
                    Namespace = ".corp.test",
                    ProfileId = "test-profile",
                    Priority = 10,
                },
            ],
        };

        var text = TrayTextFormatter.BuildOverviewDetails(
            configuration,
            status,
            new DnsHealthSettings { Enabled = true },
            new DnsHealthState { Status = DnsHealthStatus.Healthy },
            splitDnsConfiguration,
            preferredProfileId: "test-profile",
            localizer);

        Assert.Contains("Мониторинг DNS: Включено (Исправен)", text, StringComparison.Ordinal);
        Assert.Contains("Split DNS: Включено (Правил: 1)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("::", text, StringComparison.Ordinal);
        Assert.DoesNotContain("rule(s)", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildHealthDetails_UsesLocalizedLabelsAndValues()
    {
        var localizer = new AppLocalizer(AppLanguage.Russian);
        var result = new DnsHealthEvaluationResult(
            DnsHealthStatus.Failed,
            SwitchedProfile: false,
            ActiveProfileId: null,
            TargetProfileId: "fallback",
            Details: "DNS query timed out.",
            State: new DnsHealthState
            {
                Status = DnsHealthStatus.Failed,
                LastAction = null,
                LastFailureReason = "timeout",
            },
            TestResult: null);

        var text = TrayTextFormatter.BuildHealthDetails(result, localizer);

        Assert.Contains("Статус: Ошибка", text, StringComparison.Ordinal);
        Assert.Contains("Профиль переключён: Нет", text, StringComparison.Ordinal);
        Assert.Contains("Активный профиль: <нет>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Switched profile:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSplitDnsDetails_UsesLocalizedLabelsAndEmptyState()
    {
        var localizer = new AppLocalizer(AppLanguage.Russian);

        var text = TrayTextFormatter.BuildSplitDnsDetails(SplitDnsConfiguration.Default, localizer);

        Assert.Contains("Split DNS включён: Выключено", text, StringComparison.Ordinal);
        Assert.Contains("Правил: 0", text, StringComparison.Ordinal);
        Assert.Contains("Правила Split DNS не настроены.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("No Split DNS rules configured.", text, StringComparison.Ordinal);
    }

    private static AppConfig CreateConfiguration(string profileName)
    {
        return new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "test-profile",
                    Name = profileName,
                    Mode = ProfileMode.Static,
                    Ipv4 = ["1.1.1.1"],
                },
            ],
        };
    }

    private static DnsStatus CreateStatus(string? matchedProfileId, string? adapterName)
    {
        return new DnsStatus(
            IsManaged: true,
            MatchedProfileId: matchedProfileId,
            AdapterName: adapterName,
            Mode: DnsMode.Manual,
            Ipv4: new DnsServerState(DnsMode.Manual, ["1.1.1.1"]),
            Ipv6: new DnsServerState(DnsMode.Dhcp, []),
            Details: "Test status");
    }
}
