using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsProfileExchangeService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public async Task<IReadOnlyList<DnsProfile>> ImportProfilesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected profile import file was not found.", fullPath);
        }

        string jsonPayload;

        try
        {
            jsonPayload = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Failed to read profile import file '{fullPath}'.", exception);
        }

        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            throw new InvalidDataException("The selected profile import file is empty.");
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<AppConfig>(jsonPayload, SerializerOptions);

            if (configuration is not null && configuration.Profiles.Count > 0)
            {
                return configuration.Profiles;
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            var profiles = JsonSerializer.Deserialize<List<DnsProfile>>(jsonPayload, SerializerOptions);

            if (profiles is { Count: > 0 })
            {
                return profiles;
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            var profile = JsonSerializer.Deserialize<DnsProfile>(jsonPayload, SerializerOptions);

            if (profile is not null)
            {
                return [profile];
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The selected file does not contain a valid DNS profile or profiles configuration.", exception);
        }

        throw new InvalidDataException("The selected file does not contain any DNS profiles.");
    }

    public async Task ExportProfileAsync(string filePath, DnsProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(profile);

        var fullPath = PrepareExportPath(filePath);
        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, profile, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportProfilesAsync(
        string filePath,
        IReadOnlyList<DnsProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(profiles);

        var fullPath = PrepareExportPath(filePath);
        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string PrepareExportPath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return fullPath;
    }
}
