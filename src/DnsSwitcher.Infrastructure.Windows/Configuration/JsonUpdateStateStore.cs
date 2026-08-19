using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonUpdateStateStore(IAppPaths paths, ILogger<JsonUpdateStateStore> logger)
{
    public const string FileName = "update-state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public string FilePath => Path.Combine(paths.ConfigDirectory, FileName);

    public async Task<UpdateState> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);
        if (!File.Exists(FilePath))
        {
            return UpdateState.Default;
        }

        try
        {
            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<UpdateState>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? UpdateState.Default;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse update state from {UpdateStateFilePath}", FilePath);
            return UpdateState.Default;
        }
    }

    public async Task SaveAsync(UpdateState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = AtomicFileWriter.CreateTempPath(FilePath);
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        await AtomicFileWriter.MoveOverwritingWithRetryAsync(tempPath, FilePath, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Saved update state to {UpdateStateFilePath}.", FilePath);
    }
}
