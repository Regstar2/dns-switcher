using System.Text.Json;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonDnsProfileStore(IAppPaths paths, ILogger<JsonDnsProfileStore> logger) : IProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (File.Exists(paths.ProfilesFilePath))
        {
            return;
        }

        logger.LogInformation("Creating default profiles file at {ProfilesFilePath}", paths.ProfilesFilePath);
        await SaveAsync(DnsConfiguration.CreateDefault(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<DnsConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = File.OpenRead(paths.ProfilesFilePath);
        var configuration = await JsonSerializer
            .DeserializeAsync<DnsConfiguration>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return configuration ?? DnsConfiguration.CreateDefault();
    }

    public async Task SaveAsync(DnsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

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
}
