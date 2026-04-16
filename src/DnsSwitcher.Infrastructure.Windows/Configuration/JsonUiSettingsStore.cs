using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonUiSettingsStore(IAppPaths paths, ILogger<JsonUiSettingsStore> logger)
{
    public const string FileName = "ui-settings.json";

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

        logger.LogInformation("Creating UI settings file at {UiSettingsFilePath}", FilePath);
        await SaveAsync(UiSettings.Default, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UiSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var stream = File.OpenRead(FilePath);
            var settings = await JsonSerializer
                .DeserializeAsync<UiSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Loaded UI settings from {UiSettingsFilePath}. Minimize to tray: {MinimizeToTray}. Last adapter: {LastAdapterId}. Last profile: {LastSelectedProfileId}",
                FilePath,
                settings?.MinimizeToTray ?? UiSettings.Default.MinimizeToTray,
                settings?.LastAdapterId ?? "<auto>",
                settings?.LastSelectedProfileId ?? "<none>");

            return settings ?? UiSettings.Default;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse UI settings from {UiSettingsFilePath}", FilePath);
            throw new InvalidDataException($"Failed to parse UI settings: {FilePath}", exception);
        }
    }

    public async Task SaveAsync(UiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = AtomicFileWriter.CreateTempPath(FilePath);
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        await AtomicFileWriter.MoveOverwritingWithRetryAsync(tempPath, FilePath, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Saved UI settings to {UiSettingsFilePath}. Minimize to tray: {MinimizeToTray}. Last adapter: {LastAdapterId}. Last profile: {LastSelectedProfileId}",
            FilePath,
            settings.MinimizeToTray,
            settings.LastAdapterId ?? "<auto>",
            settings.LastSelectedProfileId ?? "<none>");
    }
}
