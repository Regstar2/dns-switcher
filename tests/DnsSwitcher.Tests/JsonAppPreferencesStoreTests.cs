using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonAppPreferencesStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_CreatesDefaultPreferencesFile()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonAppPreferencesStore(paths, NullLogger<JsonAppPreferencesStore>.Instance);

        var preferences = await store.LoadAsync();

        Assert.True(File.Exists(store.FilePath));
        Assert.Equal(AppLanguage.System, preferences.Language);
        Assert.Equal(AppTheme.System, preferences.Theme);
    }

    [Fact]
    public async Task SaveAsync_PersistsLanguage()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonAppPreferencesStore(paths, NullLogger<JsonAppPreferencesStore>.Instance);

        await store.SaveAsync(new AppPreferences
        {
            Language = AppLanguage.Russian,
            Theme = AppTheme.Dark,
        });

        var preferences = await store.LoadAsync();

        Assert.Equal(AppLanguage.Russian, preferences.Language);
        Assert.Equal(AppTheme.Dark, preferences.Theme);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
