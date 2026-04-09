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
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsValues()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonTraySettingsStore(paths, NullLogger<JsonTraySettingsStore>.Instance);

        await store.SaveAsync(new TraySettings
        {
            NotificationsEnabled = false,
            ShowAdapterName = false,
        });

        var settings = await store.LoadAsync();

        Assert.False(settings.NotificationsEnabled);
        Assert.False(settings.ShowAdapterName);
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
