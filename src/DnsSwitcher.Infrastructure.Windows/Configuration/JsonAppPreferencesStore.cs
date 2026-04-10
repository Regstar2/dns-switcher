using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonAppPreferencesStore(IAppPaths paths, ILogger<JsonAppPreferencesStore> logger)
{
    public const string FileName = "app-preferences.json";

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

        logger.LogInformation("Creating app preferences file at {AppPreferencesFilePath}", FilePath);
        await SaveAsync(AppPreferences.Default, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var stream = File.OpenRead(FilePath);
            var preferences = await JsonSerializer
                .DeserializeAsync<AppPreferences>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Loaded app preferences from {AppPreferencesFilePath}. Language: {Language}. Theme: {Theme}",
                FilePath,
                preferences?.Language ?? AppPreferences.Default.Language,
                preferences?.Theme ?? AppPreferences.Default.Theme);

            return preferences ?? AppPreferences.Default;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse app preferences from {AppPreferencesFilePath}", FilePath);
            throw new InvalidDataException($"Failed to parse app preferences: {FilePath}", exception);
        }
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = $"{FilePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, preferences, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            File.Move(tempPath, FilePath, overwrite: true);
            logger.LogInformation(
                "Saved app preferences to {AppPreferencesFilePath}. Language: {Language}. Theme: {Theme}",
                FilePath,
                preferences.Language,
                preferences.Theme);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
