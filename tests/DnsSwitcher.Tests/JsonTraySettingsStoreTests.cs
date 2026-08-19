using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonTraySettingsStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_CreatesDefaultSettingsFile()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);

        var settings = await store.LoadAsync();

        Assert.True(File.Exists(store.FilePath));
        Assert.True(settings.NotificationsEnabled);
        Assert.True(settings.ShowAdapterName);
        Assert.True(settings.ShowDnsActions);
        Assert.True(settings.ShowDiagnostics);
        Assert.True(settings.ShowSplitDns);
        Assert.True(settings.ShowAgent);
        Assert.True(settings.ShowProfiles);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultsForNewProperties_FromLegacyJson()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);
        Directory.CreateDirectory(paths.ConfigDirectory);
        await File.WriteAllTextAsync(store.FilePath, "{\"notificationsEnabled\":true,\"showAdapterName\":false}");

        var settings = await store.LoadAsync();

        Assert.True(settings.NotificationsEnabled);
        Assert.False(settings.ShowAdapterName);
        Assert.True(settings.ShowDnsActions);
        Assert.True(settings.ShowDiagnostics);
        Assert.True(settings.ShowSplitDns);
        Assert.True(settings.ShowAgent);
        Assert.True(settings.ShowProfiles);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsAllSettingsValues()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);
        var expected = new TraySettings
        {
            NotificationsEnabled = false,
            ShowAdapterName = false,
            ShowDnsActions = false,
            ShowDiagnostics = true,
            ShowSplitDns = false,
            ShowAgent = true,
            ShowProfiles = false,
        };

        await store.SaveAsync(expected);
        var settings = await store.LoadAsync();

        Assert.Equal(expected, settings);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultsForPropertiesMissingFromPartialJson()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);
        Directory.CreateDirectory(paths.ConfigDirectory);
        await File.WriteAllTextAsync(store.FilePath, "{\"showDiagnostics\":false,\"showProfiles\":false}");

        var settings = await store.LoadAsync();

        Assert.True(settings.NotificationsEnabled);
        Assert.True(settings.ShowAdapterName);
        Assert.True(settings.ShowDnsActions);
        Assert.False(settings.ShowDiagnostics);
        Assert.True(settings.ShowSplitDns);
        Assert.True(settings.ShowAgent);
        Assert.False(settings.ShowProfiles);
    }

    [Fact]
    public async Task LoadAsync_ThrowsInvalidDataException_ForInvalidJson()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);

        Directory.CreateDirectory(paths.ConfigDirectory);
        await File.WriteAllTextAsync(store.FilePath, "{ invalid json }");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
