using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonDnsProfileStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnsureCreatedAsync_WritesDefaultProfilesFile()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonDnsProfileStore(paths, NullLogger<JsonDnsProfileStore>.Instance);

        await store.EnsureCreatedAsync();

        Assert.True(File.Exists(paths.ProfilesFilePath));

        var configuration = await store.LoadAsync();

        Assert.Equal(1, configuration.Version);
        Assert.Contains(configuration.Profiles, profile => profile.Id == "cloudflare");
        Assert.Contains(configuration.Profiles, profile => profile.Id == "google");
        Assert.Contains(configuration.Profiles, profile => profile.Id == "dhcp" && profile.Mode == ProfileMode.Dhcp);
    }

    [Fact]
    public async Task LoadAsync_ThrowsValidationException_ForInvalidProfilesFile()
    {
        var paths = new PortableAppPaths(rootPath);
        Directory.CreateDirectory(paths.ConfigDirectory);
        await File.WriteAllTextAsync(
            paths.ProfilesFilePath,
            """
            {
              "version": 1,
              "profiles": [
                {
                  "id": "invalid",
                  "name": "",
                  "mode": "dhcp",
                  "ipv4": [
                    "1.1.1.1"
                  ],
                  "ipv6": []
                }
              ]
            }
            """);

        var store = new JsonDnsProfileStore(paths, NullLogger<JsonDnsProfileStore>.Instance);

        var exception = await Assert.ThrowsAsync<AppConfigValidationException>(() => store.LoadAsync());

        Assert.Contains(exception.Errors, error => error.Code == "EmptyProfileName");
        Assert.Contains(exception.Errors, error => error.Code == "DhcpProfileHasStaticAddresses");
    }

    [Fact]
    public async Task SaveAsync_WritesProfileModeAsString()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonDnsProfileStore(paths, NullLogger<JsonDnsProfileStore>.Instance);

        await store.SaveAsync(AppConfig.CreateDefault());

        var json = await File.ReadAllTextAsync(paths.ProfilesFilePath);

        Assert.Contains("\"mode\": \"static\"", json);
        Assert.Contains("\"mode\": \"dhcp\"", json);
    }

    [Fact]
    public void CreateDefault_UsesPortableDataDirectory()
    {
        var paths = PortableAppPaths.CreateDefault();

        Assert.EndsWith(Path.Combine("data", "config", "profiles.json"), paths.ProfilesFilePath);
        Assert.EndsWith(Path.Combine("data", "logs", "dns-switcher.log"), paths.LogFilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
