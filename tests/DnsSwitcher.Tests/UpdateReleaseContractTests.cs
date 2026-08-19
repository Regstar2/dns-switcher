using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class UpdateReleaseContractTests
{
    private static readonly Uri RepositoryUri = new("https://github.com/Regstar2/dns-switcher");

    [Fact]
    public async Task CheckForUpdatesAsync_IgnoresMalformedReleaseTag()
    {
        var release = new
        {
            tag_name = "release-next",
            draft = false,
            prerelease = false,
            html_url = "https://github.com/Regstar2/dns-switcher/releases/tag/release-next",
            published_at = "2026-08-19T10:00:00Z",
            assets = Array.Empty<object>(),
        };
        using var client = CreateClient(_ => JsonResponse(JsonSerializer.Serialize(new[] { release })));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Current, result.Status);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReportsMissingChecksumAsset()
    {
        const string tag = "v1.5.1";
        const string installerName = "DnsSwitcher-1.5.1-win-x64-setup.exe";
        var release = new
        {
            tag_name = tag,
            draft = false,
            prerelease = false,
            html_url = $"https://github.com/Regstar2/dns-switcher/releases/tag/{tag}",
            published_at = "2026-08-19T10:00:00Z",
            assets = new[]
            {
                new
                {
                    name = installerName,
                    browser_download_url = $"https://github.com/Regstar2/dns-switcher/releases/download/{tag}/{installerName}",
                },
            },
        };
        using var client = CreateClient(_ => JsonResponse(JsonSerializer.Serialize(new[] { release })));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.MissingChecksum, result.FailureKind);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_RejectsUntrustedInstallerHost()
    {
        const string tag = "v1.5.1";
        const string installerName = "DnsSwitcher-1.5.1-win-x64-setup.exe";
        var release = new
        {
            tag_name = tag,
            draft = false,
            prerelease = false,
            html_url = $"https://github.com/Regstar2/dns-switcher/releases/tag/{tag}",
            published_at = "2026-08-19T10:00:00Z",
            assets = new[]
            {
                new { name = installerName, browser_download_url = $"https://downloads.example.invalid/{installerName}" },
                new
                {
                    name = GitHubReleaseUpdateService.ChecksumFileName,
                    browser_download_url = $"https://github.com/Regstar2/dns-switcher/releases/download/{tag}/{GitHubReleaseUpdateService.ChecksumFileName}",
                },
            },
        };
        using var client = CreateClient(_ => JsonResponse(JsonSerializer.Serialize(new[] { release })));
        var service = CreateService(client);

        var result = await service.CheckForUpdatesAsync(SemanticVersion.Parse("1.5.0"));

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Equal(UpdateFailureKind.InvalidDownloadUrl, result.FailureKind);
    }

    [Fact]
    public async Task DownloadAndVerifyInstallerAsync_RejectsChecksumFileWithoutInstallerEntry()
    {
        var version = SemanticVersion.Parse("1.5.4");
        var installerName = $"DnsSwitcher-{version}-win-x64-setup.exe";
        var tag = $"v{version}";
        var update = new UpdateInfo(
            version,
            installerName,
            new Uri($"https://github.com/Regstar2/dns-switcher/releases/download/{tag}/{installerName}"),
            new Uri($"https://github.com/Regstar2/dns-switcher/releases/download/{tag}/{GitHubReleaseUpdateService.ChecksumFileName}"),
            new Uri($"https://github.com/Regstar2/dns-switcher/releases/tag/{tag}"),
            DateTimeOffset.UtcNow);
        using var client = CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith(GitHubReleaseUpdateService.ChecksumFileName, StringComparison.Ordinal)
                ? TextResponse($"{new string('A', 64)}  DnsSwitcher-{version}-win-x64.zip\n")
                : ByteResponse(Encoding.UTF8.GetBytes("installer payload")));
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<UpdateDeliveryException>(() => service.DownloadAndVerifyInstallerAsync(update));

        Assert.Equal(UpdateFailureKind.MissingChecksum, exception.Kind);
        CleanupUpdateDirectory(version);
    }

    [Fact]
    public void ApplicationMetadataProvider_MatchesDirectoryBuildPropsVersionAndRepository()
    {
        var metadata = AssemblyApplicationMetadataProvider.FromAssembly(typeof(GitHubReleaseUpdateService).Assembly);
        var propsPath = FindRepositoryFile("Directory.Build.props");
        var document = XDocument.Load(propsPath);
        var propertyGroup = document.Root!.Elements("PropertyGroup").First();
        var expectedVersion = propertyGroup.Element("Version")!.Value.Trim();
        var expectedRepository = propertyGroup.Element("RepositoryUrl")!.Value.Trim();

        Assert.Equal(expectedVersion, metadata.DisplayVersion);
        Assert.Equal(SemanticVersion.Parse(expectedVersion), metadata.Version);
        Assert.Equal(expectedRepository, metadata.RepositoryUri.AbsoluteUri.TrimEnd('/'));
    }

    private static GitHubReleaseUpdateService CreateService(HttpClient client) =>
        new(client, RepositoryUri, NullLogger<GitHubReleaseUpdateService>.Instance);

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new StubHttpMessageHandler((request, _) => Task.FromResult(handler(request))));

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
