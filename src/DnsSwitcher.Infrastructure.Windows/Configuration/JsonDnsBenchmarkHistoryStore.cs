using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsBenchmarkHistoryStore(
    PortableAppPaths paths,
    ILogger<JsonDnsBenchmarkHistoryStore> logger) : IDnsBenchmarkHistoryStore
{
    private const int MaxEntries = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<IReadOnlyList<DnsBenchmarkResult>> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();

        if (!File.Exists(paths.DnsBenchmarkHistoryFilePath))
        {
            logger.LogInformation("DNS benchmark history file does not exist yet: {HistoryPath}", paths.DnsBenchmarkHistoryFilePath);
            return [];
        }

        await using var stream = File.OpenRead(paths.DnsBenchmarkHistoryFilePath);
        var entries = await JsonSerializer.DeserializeAsync<List<DnsBenchmarkResult>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return entries ?? [];
    }

    public async Task AppendAsync(DnsBenchmarkResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var entries = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        entries.Insert(0, result);

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        paths.EnsureDirectories();
        var tempPath = AtomicFileWriter.CreateTempPath(paths.DnsBenchmarkHistoryFilePath);

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        await AtomicFileWriter
            .MoveOverwritingWithRetryAsync(tempPath, paths.DnsBenchmarkHistoryFilePath, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Saved DNS benchmark history entry. Total stored entries: {EntryCount}. Path: {HistoryPath}",
            entries.Count,
            paths.DnsBenchmarkHistoryFilePath);
    }
}
