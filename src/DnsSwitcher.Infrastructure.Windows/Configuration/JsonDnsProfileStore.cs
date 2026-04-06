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
            throw new InvalidDataException($"Failed to parse profiles.json: {paths.ProfilesFilePath}", exception);
        }

        configuration ??= AppConfig.CreateDefault();
        ValidateOrThrow(configuration);

        return configuration;
    }

    public async Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateOrThrow(configuration);

        Directory.CreateDirectory(paths.ConfigDirectory);

        var tempPath = $"{paths.ProfilesFilePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, configuration, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, paths.ProfilesFilePath, overwrite: true);
    }

    private static void ValidateOrThrow(AppConfig configuration)
    {
        var errors = AppConfigValidator.Validate(configuration);

        if (errors.Count > 0)
        {
            throw new AppConfigValidationException(errors);
        }
    }
}
