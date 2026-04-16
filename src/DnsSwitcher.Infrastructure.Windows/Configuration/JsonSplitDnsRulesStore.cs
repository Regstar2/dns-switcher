using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed class JsonSplitDnsRulesStore(IAppPaths paths, ILogger<JsonSplitDnsRulesStore> logger)
    : ISplitDnsRulesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public async Task<SplitDnsConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);

        if (!File.Exists(paths.SplitDnsRulesFilePath))
        {
            await SaveAsync(SplitDnsConfiguration.Default, cancellationToken).ConfigureAwait(false);
            return SplitDnsConfiguration.Default;
        }

        try
        {
            await using var stream = File.OpenRead(paths.SplitDnsRulesFilePath);
            return await JsonSerializer
                .DeserializeAsync<SplitDnsConfiguration>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? SplitDnsConfiguration.Default;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse Split DNS rules from {SplitDnsRulesFilePath}", paths.SplitDnsRulesFilePath);
            throw new InvalidDataException($"Failed to parse Split DNS rules: {paths.SplitDnsRulesFilePath}", exception);
        }
    }

    public async Task SaveAsync(SplitDnsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Directory.CreateDirectory(paths.ConfigDirectory);
        var tempPath = AtomicFileWriter.CreateTempPath(paths.SplitDnsRulesFilePath);

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        await AtomicFileWriter
            .MoveOverwritingWithRetryAsync(tempPath, paths.SplitDnsRulesFilePath, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Saved Split DNS rules to {SplitDnsRulesFilePath}. Enabled: {Enabled}. Rules: {RuleCount}.",
            paths.SplitDnsRulesFilePath,
            configuration.Enabled,
            configuration.Rules.Count);
    }
}
