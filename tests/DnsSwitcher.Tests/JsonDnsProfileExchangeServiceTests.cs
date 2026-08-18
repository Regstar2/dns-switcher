using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Tests;

public sealed class JsonDnsProfileExchangeServiceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));
    private readonly JsonDnsProfileExchangeService service = new();

    [Fact]
    public async Task ImportProfilesAsync_ReadsSingleProfileFile()
    {
        var filePath = Path.Combine(rootPath, "single-profile.json");
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(filePath, """
        {
          "id": "quad9",
          "name": "Quad9",
          "mode": "static",
          "ipv4": ["9.9.9.9"],
          "testDomains": ["quad9.net"],
          "testUrls": ["https://quad9.net/"]
        }
        """);

        var profiles = await service.ImportProfilesAsync(filePath);

        var profile = Assert.Single(profiles);
        Assert.Equal("quad9", profile.Id);
        Assert.Equal("Quad9", profile.Name);
        Assert.Equal(ProfileMode.Static, profile.Mode);
    }

    [Fact]
    public async Task ImportProfilesAsync_ReadsAppConfigFile()
    {
        var filePath = Path.Combine(rootPath, "profiles.json");
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(filePath, """
        {
          "version": 1,
          "profiles": [
            {
              "id": "quad9",
              "name": "Quad9",
              "mode": "static",
              "ipv4": ["9.9.9.9"]
            },
            {
              "id": "dhcp",
              "name": "Automatic DNS",
              "mode": "dhcp"
            }
          ]
        }
        """);

        var profiles = await service.ImportProfilesAsync(filePath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Id == "quad9");
        Assert.Contains(profiles, profile => profile.Id == "dhcp");
    }

    [Fact]
    public async Task ExportProfileAsync_WritesReadableJson()
    {
        var filePath = Path.Combine(rootPath, "exported-profile.json");
        var profile = new DnsProfile
        {
            Id = "quad9",
            Name = "Quad9",
            Mode = ProfileMode.Static,
            Ipv4 = ["9.9.9.9"],
            TestDomains = ["quad9.net"],
            TestUrls = ["https://quad9.net/"],
        };

        await service.ExportProfileAsync(filePath, profile);

        var json = await File.ReadAllTextAsync(filePath);

        Assert.Contains("\"id\": \"quad9\"", json);
        Assert.Contains("\"mode\": \"static\"", json);
    }

    [Fact]
    public async Task ExportProfilesAsync_WritesImportableProfileList()
    {
        var filePath = Path.Combine(rootPath, "all-profiles.json");
        var profiles = new[]
        {
            new DnsProfile
            {
                Id = "quad9",
                Name = "Quad9",
                Mode = ProfileMode.Static,
                Ipv4 = ["9.9.9.9"],
            },
            new DnsProfile
            {
                Id = "dhcp",
                Name = "Automatic DNS",
                Mode = ProfileMode.Dhcp,
            },
        };

        await service.ExportProfilesAsync(filePath, profiles);

        var importedProfiles = await service.ImportProfilesAsync(filePath);

        Assert.Equal(2, importedProfiles.Count);
        Assert.Contains(importedProfiles, profile => profile.Id == "quad9");
        Assert.Contains(importedProfiles, profile => profile.Id == "dhcp");
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
