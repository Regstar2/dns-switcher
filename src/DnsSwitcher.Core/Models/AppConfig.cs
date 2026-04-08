namespace DnsSwitcher.Core.Models;

public sealed record AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string? ActiveProfileId { get; init; }

    public List<DnsProfile> Profiles { get; init; } = [];

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "cloudflare",
                    Name = "Cloudflare",
                    Description = "Cloudflare public DNS",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["1.1.1.1", "1.0.0.1"],
                    Ipv6 = ["2606:4700:4700::1111", "2606:4700:4700::1001"],
                    Tags = ["public", "general"],
                    TestDomains = ["cloudflare.com", "openai.com"],
                    TestUrls = ["https://cloudflare.com/", "https://openai.com/"],
                },
                new DnsProfile
                {
                    Id = "google",
                    Name = "Google Public DNS",
                    Description = "Google public DNS",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["8.8.8.8", "8.8.4.4"],
                    Ipv6 = ["2001:4860:4860::8888", "2001:4860:4860::8844"],
                    Tags = ["public", "general"],
                    TestDomains = ["google.com", "github.com"],
                    TestUrls = ["https://google.com/", "https://github.com/"],
                },
                new DnsProfile
                {
                    Id = "dhcp",
                    Name = "Automatic DNS",
                    Description = "Use DNS servers from DHCP.",
                    Mode = ProfileMode.Dhcp,
                },
            ],
        };
    }
}
