using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsHealthStateStore(IAppPaths paths, ILogger<JsonDnsHealthStateStore> logger)
    : IDnsHealthStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public async Task<DnsHealthState> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (!File.Exists(paths.DnsHealthStateFilePath))
        {
            return new DnsHealthState();
        }

        try
        {
            await using var stream = File.OpenRead(paths.DnsHealthStateFilePath);
            return await JsonSerializer
                .DeserializeAsync<DnsHealthState>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? new DnsHealthState();
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse DNS health state from {DnsHealthStateFilePath}", paths.DnsHealthStateFilePath);
            return new DnsHealthState
            {
                Status = DnsHealthStatus.Degraded,
                LastAction = $"Failed to parse DNS health state: {exception.Message}",
            };
        }
    }

    public async Task SaveAsync(DnsHealthState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        Directory.CreateDirectory(paths.ConfigDirectory);
        var tempPath = AtomicFileWriter.CreateTempPath(paths.DnsHealthStateFilePath);

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        await AtomicFileWriter
            .MoveOverwritingWithRetryAsync(tempPath, paths.DnsHealthStateFilePath, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Saved DNS health state to {DnsHealthStateFilePath}. Status: {Status}. Active profile: {ActiveProfileId}.",
            paths.DnsHealthStateFilePath,
            state.Status,
            state.ActiveProfileId ?? "<none>");
    }
}
