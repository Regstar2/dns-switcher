using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsProfileStore(IAppPaths paths, ILogger<JsonDnsProfileStore> logger) : IProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private DateTime lastLoggedWriteUtc = DateTime.MinValue;
    private string? lastLoggedActiveProfileId;
    private int lastLoggedProfileCount = -1;

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (File.Exists(paths.ProfilesFilePath))
        {
            return;
        }

        logger.LogInformation("Creating default profiles file at {ProfilesFilePath}", paths.ProfilesFilePath);
        await SaveAsync(AppConfig.CreateDefault(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        AppConfig? configuration;

        try
        {
            await using var stream = File.OpenRead(paths.ProfilesFilePath);
            configuration = await JsonSerializer
                .DeserializeAsync<AppConfig>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Failed to parse profiles configuration from {ProfilesFilePath}", paths.ProfilesFilePath);
            throw new InvalidDataException($"Failed to parse profiles.json: {paths.ProfilesFilePath}", exception);
        }

        configuration ??= AppConfig.CreateDefault();
        ValidateOrThrow(configuration);
        LogConfigurationLoaded(configuration);

        return configuration;
    }

    public async Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateOrThrow(configuration);

        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = AtomicFileWriter.CreateTempPath(paths.ProfilesFilePath);
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, configuration, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        await AtomicFileWriter
            .MoveOverwritingWithRetryAsync(tempPath, paths.ProfilesFilePath, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Saved profiles configuration to {ProfilesFilePath}. Profiles: {ProfileCount}. Active profile: {ActiveProfileId}",
            paths.ProfilesFilePath,
            configuration.Profiles.Count,
            configuration.ActiveProfileId ?? "<none>");
    }

    private void ValidateOrThrow(AppConfig configuration)
    {
        var errors = AppConfigValidator.Validate(configuration);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "profiles.json validation failed for {ProfilesFilePath}. Errors: {ErrorCount}",
                paths.ProfilesFilePath,
                errors.Count);
            throw new AppConfigValidationException(errors);
        }
    }

    private void LogConfigurationLoaded(AppConfig configuration)
    {
        var currentWriteUtc = File.Exists(paths.ProfilesFilePath)
            ? File.GetLastWriteTimeUtc(paths.ProfilesFilePath)
            : DateTime.MinValue;

        if (currentWriteUtc == lastLoggedWriteUtc
            && configuration.ActiveProfileId == lastLoggedActiveProfileId
            && configuration.Profiles.Count == lastLoggedProfileCount)
        {
            return;
        }

        logger.LogInformation(
            "Loaded profiles configuration from {ProfilesFilePath}. Profiles: {ProfileCount}. Active profile: {ActiveProfileId}",
            paths.ProfilesFilePath,
            configuration.Profiles.Count,
            configuration.ActiveProfileId ?? "<none>");

        lastLoggedWriteUtc = currentWriteUtc;
        lastLoggedActiveProfileId = configuration.ActiveProfileId;
        lastLoggedProfileCount = configuration.Profiles.Count;
    }
}
