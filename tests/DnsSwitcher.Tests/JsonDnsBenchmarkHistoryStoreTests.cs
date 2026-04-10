using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.Tests;

public sealed class JsonDnsBenchmarkHistoryStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "DnsSwitcher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AppendAsync_PersistsNewestEntriesFirst_AndTrimsHistory()
    {
        var paths = new PortableAppPaths(rootPath);
        var store = new JsonDnsBenchmarkHistoryStore(paths, NullLogger<JsonDnsBenchmarkHistoryStore>.Instance);

        for (var index = 0; index < 12; index++)
        {
            await store.AppendAsync(CreateResult(index));
        }

        var entries = await store.LoadAsync();

        Assert.Equal(10, entries.Count);
        Assert.Equal("profile-11", entries[0].BestProfileId);
        Assert.Equal("profile-2", entries[^1].BestProfileId);
        Assert.True(File.Exists(paths.DnsBenchmarkHistoryFilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static DnsBenchmarkResult CreateResult(int index)
    {
        var testResult = new DnsTestResult(
            AdapterName: "Wi-Fi",
            ProfileId: $"profile-{index}",
            ProfileName: $"Profile {index}",
            DnsServers: ["1.1.1.1"],
            Domains: ["example.com"],
            DomainResults:
            [
                new DnsDomainTestResult(
                    Domain: "example.com",
                    Status: DnsTestStatus.Ok,
                    SuccessfulAttempts: 3,
                    TotalAttempts: 3,
                    AverageLatency: TimeSpan.FromMilliseconds(20 + index),
                    BestLatency: TimeSpan.FromMilliseconds(18 + index),
                    Details: "Resolved."),
            ],
            Status: DnsTestStatus.Ok,
            AverageLatency: TimeSpan.FromMilliseconds(20 + index),
            Details: "Status Ok.");

        return new DnsBenchmarkResult(
            ExecutedAtUtc: DateTimeOffset.UtcNow.AddMinutes(index),
            AdapterName: "Wi-Fi",
            TotalProfiles: 1,
            ProfileResults:
            [
                new DnsBenchmarkProfileResult(
                    ProfileId: $"profile-{index}",
                    ProfileName: $"Profile {index}",
                    TestResult: testResult,
                    IsBest: true),
            ],
            BestProfileId: $"profile-{index}",
            BestProfileName: $"Profile {index}",
            OverallStatus: DnsTestStatus.Ok,
            BestLatency: TimeSpan.FromMilliseconds(20 + index),
            RestoreSucceeded: true,
            RestoreDetails: "Restored.",
            WasInterrupted: false,
            InterruptionReason: null,
            Details: "Done.");
    }
}
