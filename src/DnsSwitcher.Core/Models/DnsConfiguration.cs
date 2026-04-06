namespace DnsSwitcher.Core.Models;

public sealed record DnsConfiguration
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string? ActiveProfileId { get; init; }

    public List<DnsProfile> Profiles { get; init; } = [];

    public static DnsConfiguration CreateDefault()
    {
        return new DnsConfiguration
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "cloudflare",
                    Name = "Cloudflare",
                    Description = "Cloudflare public DNS",
                    Ipv4 = ["1.1.1.1", "1.0.0.1"],
                    Ipv6 = ["2606:4700:4700::1111", "2606:4700:4700::1001"],
                },
                new DnsProfile
                {
                    Id = "google",
                    Name = "Google Public DNS",
                    Description = "Google public DNS",
                    Ipv4 = ["8.8.8.8", "8.8.4.4"],
                    Ipv6 = ["2001:4860:4860::8888", "2001:4860:4860::8844"],
                },
            ],
        };
    }
}
