using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonUiSettingsStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_CreatesDefaultSettingsFile()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonUiSettingsStore(paths, NullLogger<JsonUiSettingsStore>.Instance);

        var settings = await store.LoadAsync();

        Assert.True(File.Exists(store.FilePath));
        Assert.False(settings.MinimizeToTray);
        Assert.Null(settings.LastAdapterId);
        Assert.Null(settings.LastSelectedProfileId);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsValues()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonUiSettingsStore(paths, NullLogger<JsonUiSettingsStore>.Instance);

        await store.SaveAsync(new UiSettings
        {
            MinimizeToTray = true,
            LastAdapterId = "adapter-1",
            LastSelectedProfileId = "google",
        });

        var settings = await store.LoadAsync();

        Assert.True(settings.MinimizeToTray);
        Assert.Equal("adapter-1", settings.LastAdapterId);
        Assert.Equal("google", settings.LastSelectedProfileId);
    }

    [Fact]
    public async Task LoadAsync_ThrowsInvalidDataException_ForInvalidJson()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonUiSettingsStore(paths, NullLogger<JsonUiSettingsStore>.Instance);

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
