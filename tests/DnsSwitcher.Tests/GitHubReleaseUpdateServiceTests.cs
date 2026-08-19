using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    private static readonly Uri RepositoryUri = new("https://github.com/Regstar2/dns-switcher");

    [Fact]
    public async Task CheckForUpdatesAsync_SelectsNewestStableReleaseAndIgnoresDraftAndPrerelease()
    {
        var responseJson = BuildReleasesJson(
            Release("v1.7.0", draft: true, prerelease: false),
            Release("v1.6.0-beta.1", draft: false, prerelease: true),
            Release("v1.5.1", draft: false, prerelease: false));
        using var client = CreateClient((_, _) => JsonResponse(responseJson));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(SemanticVersion.Parse("1.5.1"), result.Update?.Version);
        Assert.Equal("DnsSwitcher-1.5.1-win-x64-setup.exe", result.Update?.InstallerFileName);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsCurrentWhenLatestStableIsInstalled()
    {
        using var client = CreateClient((_, _) => JsonResponse(BuildReleasesJson(Release("v1.5.0", false, false))));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Current, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_RejectsReleaseWithoutExpectedArchitectureInstaller()
    {
        var release = Release("v1.5.1", false, false, installerName: "DnsSwitcher-1.5.1-win-arm64-setup.exe");
        using var client = CreateClient((_, _) => JsonResponse(BuildReleasesJson(release)));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.MissingInstaller, result.FailureKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckForUpdatesAsync_TreatsHttpFailuresAsNonFatal(HttpStatusCode statusCode)
    {
        using var client = CreateClient((_, _) => new HttpResponseMessage(statusCode));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.Network, result.FailureKind);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReportsMalformedJson()
    {
        using var client = CreateClient((_, _) => JsonResponse("{not-json"));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.InvalidRelease, result.FailureKind);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_PropagatesCallerCancellation()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ConvertsHttpClientTimeoutToNetworkFailure()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return JsonResponse("[]");
        });
        client.Timeout = TimeSpan.FromMilliseconds(20);
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.Network, result.FailureKind);
    }

    [Fact]
    public async Task DownloadAndVerifyInstallerAsync_ReturnsInstallerWhenChecksumMatches()
    {
        var version = SemanticVersion.Parse("1.5.1");
        var installerName = $"DnsSwitcher-{version}-win-x64-setup.exe";
        var installerBytes = Encoding.UTF8.GetBytes("verified installer payload");
        var hash = Convert.ToHexString(SHA256.HashData(installerBytes));
        var update = CreateUpdateInfo(version, installerName);
        using var client = CreateClient((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith(GitHubReleaseUpdateService.ChecksumFileName, StringComparison.Ordinal)
                ? TextResponse($"{hash}  {installerName}\n")
                : ByteResponse(installerBytes));
        var service = CreateService(client);

        var installerPath = await service.DownloadAndVerifyInstallerAsync(update);

        try
        {
            Assert.True(File.Exists(installerPath));
            Assert.Equal(installerBytes, await File.ReadAllBytesAsync(installerPath));
        }
        finally
        {
            CleanupUpdateDirectory(version);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyInstallerAsync_RejectsChecksumMismatch()
    {
        var version = SemanticVersion.Parse("1.5.2");
        var installerName = $"DnsSwitcher-{version}-win-x64-setup.exe";
        var update = CreateUpdateInfo(version, installerName);
        using var client = CreateClient((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith(GitHubReleaseUpdateService.ChecksumFileName, StringComparison.Ordinal)
                ? TextResponse($"{new string('A', 64)}  {installerName}\n")
                : ByteResponse(Encoding.UTF8.GetBytes("different payload")));
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<UpdateDeliveryException>(() => service.DownloadAndVerifyInstallerAsync(update));

        Assert.Equal(UpdateFailureKind.ChecksumMismatch, exception.Kind);
        CleanupUpdateDirectory(version);
    }

    [Fact]
    public async Task DownloadAndVerifyInstallerAsync_RejectsInvalidChecksumFile()
    {
        var version = SemanticVersion.Parse("1.5.3");
        var installerName = $"DnsSwitcher-{version}-win-x64-setup.exe";
        var update = CreateUpdateInfo(version, installerName);
        using var client = CreateClient((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith(GitHubReleaseUpdateService.ChecksumFileName, StringComparison.Ordinal)
                ? TextResponse($"NOT-A-HASH  {installerName}\n")
                : ByteResponse(Encoding.UTF8.GetBytes("payload")));
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<UpdateDeliveryException>(() => service.DownloadAndVerifyInstallerAsync(update));

        Assert.Equal(UpdateFailureKind.ChecksumInvalid, exception.Kind);
        CleanupUpdateDirectory(version);
    }

    [Fact]
    public void Source_DoesNotContainEmbeddedAuthorizationHeader()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "DnsSwitcher.Infrastructure.Windows", "Updates", "GitHubReleaseUpdateService.cs"));

        Assert.DoesNotContain("Authorization", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", source, StringComparison.OrdinalIgnoreCase);
    }

    private static GitHubReleaseUpdateService CreateService(HttpClient client) =>
        new(client, RepositoryUri, NullLogger<GitHubReleaseUpdateService>.Instance);

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) =>
        new(new StubHttpMessageHandler((request, token) => Task.FromResult(handler(request, token))));

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new StubHttpMessageHandler(handler));

    private static object Release(
        string tag,
        bool draft,
        bool prerelease,
        string? installerName = null)
    {
        var normalizedVersion = SemanticVersion.TryParse(tag, out var parsed) ? parsed.ToString() : "1.0.0";
        installerName ??= $"DnsSwitcher-{normalizedVersion}-win-x64-setup.exe";
        var baseUrl = $"https://github.com/Regstar2/dns-switcher/releases/download/{tag}";
        return new
        {
            tag_name = tag,
            draft,
            prerelease,
            html_url = $"https://github.com/Regstar2/dns-switcher/releases/tag/{tag}",
            published_at = "2026-08-19T10:00:00Z",
            assets = new[]
            {
                new { name = installerName, browser_download_url = $"{baseUrl}/{installerName}" },
                new { name = GitHubReleaseUpdateService.ChecksumFileName, browser_download_url = $"{baseUrl}/{GitHubReleaseUpdateService.ChecksumFileName}" },
            },
        };
    }

    private static string BuildReleasesJson(params object[] releases) => JsonSerializer.Serialize(releases);

    private static UpdateInfo CreateUpdateInfo(SemanticVersion version, string installerName)
    {
        var tag = $"v{version}";
        var baseUrl = $"https://github.com/Regstar2/dns-switcher/releases/download/{tag}";
        return new UpdateInfo(
            version,
            installerName,
            new Uri($"{baseUrl}/{installerName}"),
            new Uri($"{baseUrl}/{GitHubReleaseUpdateService.ChecksumFileName}"),
            new Uri($"https://github.com/Regstar2/dns-switcher/releases/tag/{tag}"),
            DateTimeOffset.UtcNow);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TextResponse(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/plain") };

    private static HttpResponseMessage ByteResponse(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private static void CleanupUpdateDirectory(SemanticVersion version)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DnsSwitcher", "updates", version.ToString());
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Repository file was not found: {Path.Combine(pathSegments)}");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
