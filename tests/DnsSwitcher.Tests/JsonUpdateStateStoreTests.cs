using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonUpdateStateStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ReturnsDefaultWhenStateFileDoesNotExist()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonUpdateStateStore(paths, NullLogger<JsonUpdateStateStore>.Instance);

        var state = await store.LoadAsync();

        Assert.Null(state.LastCheckedUtc);
        Assert.Null(state.LastNotifiedVersion);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsUpdateState()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonUpdateStateStore(paths, NullLogger<JsonUpdateStateStore>.Instance);
        var checkedAt = new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

        await store.SaveAsync(new UpdateState
        {
            LastCheckedUtc = checkedAt,
            LastNotifiedVersion = "1.5.1",
        });
        var loaded = await store.LoadAsync();

        Assert.Equal(checkedAt, loaded.LastCheckedUtc);
        Assert.Equal("1.5.1", loaded.LastNotifiedVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
