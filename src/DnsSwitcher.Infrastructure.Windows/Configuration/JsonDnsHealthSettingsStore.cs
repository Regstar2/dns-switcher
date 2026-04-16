using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsHealthSettingsStore(IAppPaths paths, ILogger<JsonDnsHealthSettingsStore> logger)
    : IDnsHealthSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public async Task<DnsHealthSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (!File.Exists(paths.DnsHealthSettingsFilePath))
        {
            await SaveAsync(DnsHealthSettings.Default, cancellationToken).ConfigureAwait(false);
            return DnsHealthSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(paths.DnsHealthSettingsFilePath);
            return await JsonSerializer
                .DeserializeAsync<DnsHealthSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? DnsHealthSettings.Default;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse DNS health settings from {DnsHealthSettingsFilePath}", paths.DnsHealthSettingsFilePath);
            throw new InvalidDataException($"Failed to parse DNS health settings: {paths.DnsHealthSettingsFilePath}", exception);
        }
    }

    public async Task SaveAsync(DnsHealthSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(paths.ConfigDirectory);
        var tempPath = AtomicFileWriter.CreateTempPath(paths.DnsHealthSettingsFilePath);

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        await AtomicFileWriter
            .MoveOverwritingWithRetryAsync(tempPath, paths.DnsHealthSettingsFilePath, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Saved DNS health settings to {DnsHealthSettingsFilePath}. Enabled: {Enabled}. Action: {ActionOnFailure}.",
            paths.DnsHealthSettingsFilePath,
            settings.Enabled,
            settings.ActionOnFailure);
    }
}
