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
