using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class DiagnosticTextFormatter
{
    public static string BuildDnsStatusSummary(DnsTestResult result)
    {
        var parts = new List<string>
        {
            $"DNS test {result.Status}",
            $"domains: {result.Domains.Count}",
            $"servers: {result.DnsServers.Count}",
            $"avg latency: {FormatLatency(result.AverageLatency)}",
        };

        if (result.DomainResults.Count > 0)
        {
            parts.Add(string.Join(
                "; ",
                result.DomainResults.Select(domainResult =>
                    $"{domainResult.Domain}: {domainResult.Status} ({domainResult.SuccessfulAttempts}/{domainResult.TotalAttempts})")));
        }

        return string.Join(" | ", parts);
    }

    public static string BuildDnsBalloonSummary(DnsTestResult result)
    {
        return
            $"DNS test {result.Status}. " +
            $"Domains: {result.Domains.Count}. " +
            $"Average latency: {FormatLatency(result.AverageLatency)}.";
    }

    public static string BuildDnsDetails(DnsTestResult result)
    {
        var lines = new List<string>
        {
            $"Status: {result.Status}",
            $"Adapter: {result.AdapterName ?? "<none>"}",
            $"Profile: {FormatProfileLabel(result.ProfileName, result.ProfileId)}",
            $"DNS servers: {(result.DnsServers.Count == 0 ? "<none>" : string.Join(", ", result.DnsServers))}",
            $"Average latency: {FormatLatency(result.AverageLatency)}",
            string.Empty,
            "Domains:",
        };

        if (result.DomainResults.Count == 0)
        {
            lines.Add("  <none>");
        }
        else
        {
            foreach (var domainResult in result.DomainResults)
            {
                lines.Add(
                    $"  {domainResult.Domain}: {domainResult.Status} | " +
                    $"{domainResult.SuccessfulAttempts}/{domainResult.TotalAttempts} | " +
                    $"avg {FormatLatency(domainResult.AverageLatency)}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(result.Details);
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildSiteStatusSummary(ConnectivityTestResult result)
    {
        var parts = new List<string>
        {
            $"Site test {result.Status}",
            $"urls: {result.Urls.Count}",
            $"avg latency: {FormatLatency(result.AverageLatency)}",
        };

        if (result.UrlResults.Count > 0)
        {
            parts.Add(string.Join(
                "; ",
                result.UrlResults.Select(urlResult =>
                    $"{urlResult.Url}: {urlResult.Status} ({urlResult.SuccessfulAttempts}/{urlResult.TotalAttempts})")));
        }

        return string.Join(" | ", parts);
    }

    public static string BuildSiteBalloonSummary(ConnectivityTestResult result)
    {
        return
            $"Site test {result.Status}. " +
            $"URLs: {result.Urls.Count}. " +
            $"Average latency: {FormatLatency(result.AverageLatency)}.";
    }

    public static string BuildSiteDetails(ConnectivityTestResult result)
    {
        var lines = new List<string>
        {
            $"Status: {result.Status}",
            $"Adapter: {result.AdapterName ?? "<none>"}",
            $"Profile: {FormatProfileLabel(result.ProfileName, result.ProfileId)}",
            $"Average latency: {FormatLatency(result.AverageLatency)}",
            string.Empty,
        };

        if (result.UrlResults.Count == 0)
        {
            lines.Add(result.Details);
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var urlResult in result.UrlResults)
        {
            lines.Add($"{urlResult.Url}");
            lines.Add($"  Status: {urlResult.Status}");
            lines.Add($"  Attempts: {urlResult.SuccessfulAttempts}/{urlResult.TotalAttempts}");
            lines.Add($"  HTTP: {(urlResult.HttpStatusCode?.ToString() ?? "<none>")} via {urlResult.HttpMethod}");
            lines.Add($"  DNS: {urlResult.Dns.Details}");
            lines.Add($"  TCP: {urlResult.Connect.Details}");

            if (!string.Equals(urlResult.Tls.Details, "TLS not required.", StringComparison.Ordinal))
            {
                lines.Add($"  TLS: {urlResult.Tls.Details}");
            }

            lines.Add($"  HTTP details: {urlResult.Http.Details}");
            lines.Add($"  Summary: {urlResult.Details}");
            lines.Add(string.Empty);
        }

        lines.Add(result.Details);
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildBenchmarkStatusSummary(DnsBenchmarkResult result)
    {
        var parts = new List<string>
        {
            $"Benchmark {result.OverallStatus}",
            $"tested: {result.ProfileResults.Count}/{result.TotalProfiles}",
            $"best: {FormatProfileLabel(result.BestProfileName, result.BestProfileId)}",
            $"best latency: {FormatLatency(result.BestLatency)}",
        };

        if (result.WasInterrupted)
        {
            parts.Add($"interrupted: {result.InterruptionReason}");
        }

        if (!result.RestoreSucceeded)
        {
            parts.Add("restore failed");
        }

        return string.Join(" | ", parts);
    }

    public static string BuildBenchmarkBalloonSummary(DnsBenchmarkResult result)
    {
        return
            $"Benchmark {result.OverallStatus}. " +
            $"Best: {FormatProfileLabel(result.BestProfileName, result.BestProfileId)}. " +
            $"Latency: {FormatLatency(result.BestLatency)}.";
    }

    public static string BuildBenchmarkDetails(DnsBenchmarkResult result)
    {
        var lines = new List<string>
        {
            $"Status: {result.OverallStatus}",
            $"Adapter: {result.AdapterName ?? "<none>"}",
            $"Executed: {result.ExecutedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
            $"Tested profiles: {result.ProfileResults.Count}/{result.TotalProfiles}",
            $"Best profile: {FormatProfileLabel(result.BestProfileName, result.BestProfileId)}",
            $"Best latency: {FormatLatency(result.BestLatency)}",
            $"Restore: {(result.RestoreSucceeded ? "OK" : "Failed")} - {result.RestoreDetails}",
        };

        if (result.WasInterrupted)
        {
            lines.Add($"Interrupted: {result.InterruptionReason ?? "<none>"}");
        }

        lines.Add(string.Empty);
        lines.Add("Profiles:");

        if (result.ProfileResults.Count == 0)
        {
            lines.Add("  <none>");
        }
        else
        {
            foreach (var profileResult in result.ProfileResults)
            {
                var bestMarker = profileResult.IsBest ? " [best]" : string.Empty;
                lines.Add(
                    $"  {profileResult.ProfileName} ({profileResult.ProfileId}){bestMarker}: " +
                    $"{profileResult.TestResult.Status} | avg {FormatLatency(profileResult.TestResult.AverageLatency)}");
                lines.Add($"    {profileResult.TestResult.Details}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(result.Details);
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatLatency(TimeSpan? latency)
    {
        return latency is null
            ? "n/a"
            : $"{Math.Round(latency.Value.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }

    public static string FormatProfileLabel(string? profileName, string? profileId)
    {
        return string.IsNullOrWhiteSpace(profileId)
            ? "<none>"
            : string.IsNullOrWhiteSpace(profileName)
                ? profileId
                : $"{profileName} ({profileId})";
    }
}
