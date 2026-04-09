using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonTraySettingsStore(IAppPaths paths, ILogger<JsonTraySettingsStore> logger)
{
    public const string FileName = "tray-settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public string FilePath => Path.Combine(paths.ConfigDirectory, FileName);

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (File.Exists(FilePath))
        {
            return;
        }

        logger.LogInformation("Creating tray settings file at {TraySettingsFilePath}", FilePath);
        await SaveAsync(TraySettings.Default, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TraySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var stream = File.OpenRead(FilePath);
            var settings = await JsonSerializer
                .DeserializeAsync<TraySettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return settings ?? TraySettings.Default;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Failed to parse tray settings: {FilePath}", exception);
        }
    }

    public async Task SaveAsync(TraySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = $"{FilePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, FilePath, overwrite: true);
    }
}
