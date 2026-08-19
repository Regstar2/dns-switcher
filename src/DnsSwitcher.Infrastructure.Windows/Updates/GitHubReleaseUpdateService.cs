using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Updates;

public sealed class GitHubReleaseUpdateService : IUpdateService
{
    public const string ChecksumFileName = "SHA256SUMS.txt";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly ILogger<GitHubReleaseUpdateService> logger;
    private readonly string owner;
    private readonly string repository;
    private readonly Uri releasesApiUri;

    public GitHubReleaseUpdateService(
        HttpClient httpClient,
        Uri repositoryUri,
        ILogger<GitHubReleaseUpdateService> logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        (owner, repository) = ParseGitHubRepository(repositoryUri);
        releasesApiUri = new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases?per_page=30");
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        SemanticVersion currentVersion,
        UpdateChannel channel = UpdateChannel.Stable,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient
                .GetAsync(releasesApiUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub release check failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.Network);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var releases = await JsonSerializer
                .DeserializeAsync<List<GitHubReleaseDto>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? [];

            var candidates = releases
                .Where(release => !release.Draft)
                .Select(release => TryCreateCandidate(release, channel))
                .Where(candidate => candidate is not null)
                .Cast<ReleaseCandidate>()
                .OrderByDescending(candidate => candidate.Version)
                .ToArray();

            if (candidates.Length == 0 || candidates[0].Version <= currentVersion)
            {
                return UpdateCheckResult.Current(currentVersion);
            }

            var latest = candidates[0];
            var installerFileName = $"DnsSwitcher-{latest.Version}-win-x64-setup.exe";
            var installerAsset = latest.Release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, installerFileName, StringComparison.OrdinalIgnoreCase));
            if (installerAsset is null)
            {
                return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.MissingInstaller);
            }

            var checksumAsset = latest.Release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, ChecksumFileName, StringComparison.OrdinalIgnoreCase));
            if (checksumAsset is null)
            {
                return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.MissingChecksum);
            }

            if (!TryGetTrustedAssetUri(installerAsset.DownloadUrl, out var installerUri)
                || !TryGetTrustedAssetUri(checksumAsset.DownloadUrl, out var checksumUri)
                || !TryGetTrustedReleasePageUri(latest.Release.HtmlUrl, out var releasePageUri))
            {
                return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.InvalidDownloadUrl);
            }

            var update = new UpdateInfo(
                latest.Version,
                installerFileName,
                installerUri,
                checksumUri,
                releasePageUri,
                latest.Release.PublishedAt);
            return UpdateCheckResult.Available(currentVersion, update);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("GitHub release check timed out.");
            return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.Network);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "GitHub release check failed.");
            return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.Network);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "GitHub release response was not valid JSON.");
            return UpdateCheckResult.Unavailable(currentVersion, UpdateFailureKind.InvalidRelease);
        }
    }

    public async Task<string> DownloadAndVerifyInstallerAsync(
        UpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!TryGetTrustedAssetUri(update.InstallerUri.AbsoluteUri, out _)
            || !TryGetTrustedAssetUri(update.ChecksumUri.AbsoluteUri, out _))
        {
            throw new UpdateDeliveryException(UpdateFailureKind.InvalidDownloadUrl, "Release asset URL is not trusted.");
        }

        var expectedFileName = $"DnsSwitcher-{update.Version}-win-x64-setup.exe";
        if (!string.Equals(update.InstallerFileName, expectedFileName, StringComparison.Ordinal))
        {
            throw new UpdateDeliveryException(UpdateFailureKind.MissingInstaller, "Release installer filename does not match the expected Windows x64 asset.");
        }

        var updateDirectory = Path.Combine(Path.GetTempPath(), "DnsSwitcher", "updates", update.Version.ToString());
        Directory.CreateDirectory(updateDirectory);
        var installerPath = Path.Combine(updateDirectory, expectedFileName);
        var checksumPath = Path.Combine(updateDirectory, ChecksumFileName);

        try
        {
            await DownloadFileAsync(update.ChecksumUri, checksumPath, cancellationToken).ConfigureAwait(false);
            var expectedHash = await ReadExpectedHashAsync(checksumPath, expectedFileName, cancellationToken).ConfigureAwait(false);
            await DownloadFileAsync(update.InstallerUri, installerPath, cancellationToken).ConfigureAwait(false);
            var actualHash = await ComputeSha256Async(installerPath, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(installerPath);
                throw new UpdateDeliveryException(UpdateFailureKind.ChecksumMismatch, "Downloaded installer SHA-256 does not match the published checksum.");
            }

            return installerPath;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateDeliveryException(UpdateFailureKind.Network, "Update download timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateDeliveryException(UpdateFailureKind.Network, "Update download failed.", exception);
        }
    }

    public void LaunchInstaller(string installerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);

        var fullPath = Path.GetFullPath(installerPath);
        var trustedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DnsSwitcher", "updates")) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            throw new UpdateDeliveryException(UpdateFailureKind.InvalidDownloadUrl, "Installer path is outside the trusted update directory.");
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                Arguments = "/CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas",
            });

            if (process is null)
            {
                throw new UpdateDeliveryException(UpdateFailureKind.LaunchFailed, "Windows did not start the installer process.");
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new UpdateDeliveryException(UpdateFailureKind.LaunchCancelled, "Installer elevation was cancelled by the user.", exception);
        }
        catch (Win32Exception exception)
        {
            throw new UpdateDeliveryException(UpdateFailureKind.LaunchFailed, "Failed to start the installer.", exception);
        }
    }

    private ReleaseCandidate? TryCreateCandidate(GitHubReleaseDto release, UpdateChannel channel)
    {
        if (!SemanticVersion.TryParse(release.TagName, out var version))
        {
            logger.LogDebug("Ignoring release with malformed semantic tag {TagName}.", release.TagName);
            return null;
        }

        if (channel == UpdateChannel.Stable && (release.Prerelease || version.IsPrerelease))
        {
            return null;
        }

        return new ReleaseCandidate(release, version);
    }

    private async Task DownloadFileAsync(Uri uri, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadExpectedHashAsync(
        string checksumPath,
        string expectedFileName,
        CancellationToken cancellationToken)
    {
        foreach (var line in await File.ReadAllLinesAsync(checksumPath, cancellationToken).ConfigureAwait(false))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var fileName = parts[^1].TrimStart('*');
            if (!string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
            {
                continue;
            }

            var hash = parts[0];
            if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new UpdateDeliveryException(UpdateFailureKind.ChecksumInvalid, "Published SHA-256 checksum is invalid.");
            }

            return hash.ToUpperInvariant();
        }

        throw new UpdateDeliveryException(UpdateFailureKind.MissingChecksum, "Published checksum file does not contain the installer entry.");
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private bool TryGetTrustedAssetUri(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requiredPrefix = $"/{owner}/{repository}/releases/download/";
        if (!candidate.AbsolutePath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private bool TryGetTrustedReleasePageUri(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requiredPrefix = $"/{owner}/{repository}/releases/";
        if (!candidate.AbsolutePath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static (string Owner, string Repository) ParseGitHubRepository(Uri repositoryUri)
    {
        ArgumentNullException.ThrowIfNull(repositoryUri);
        if (repositoryUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(repositoryUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Repository URL must use https://github.com/owner/repository.", nameof(repositoryUri));
        }

        var segments = repositoryUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            throw new ArgumentException("Repository URL must contain exactly owner and repository segments.", nameof(repositoryUri));
        }

        return (segments[0], segments[1]);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ReleaseCandidate(GitHubReleaseDto Release, SemanticVersion Version);

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto> Assets { get; init; } = [];
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; init; } = string.Empty;
    }
}
