using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class DnsBenchmarkSelector
{
    public DnsBenchmarkProfileResult? SelectBestProfile(IReadOnlyList<DnsBenchmarkProfileResult> profileResults)
    {
        ArgumentNullException.ThrowIfNull(profileResults);

        var candidates = profileResults
            .Where(result => result.TestResult.Status is DnsTestStatus.Ok or DnsTestStatus.Slow)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        return candidates
            .OrderBy(GetStatusRank)
            .ThenByDescending(GetHealthyDomainCount)
            .ThenByDescending(GetOkDomainCount)
            .ThenBy(GetAverageLatencyOrMax)
            .ThenBy(result => result.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.ProfileId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int GetStatusRank(DnsBenchmarkProfileResult result)
    {
        return result.TestResult.Status switch
        {
            DnsTestStatus.Ok => 0,
            DnsTestStatus.Slow => 1,
            DnsTestStatus.Failed => 2,
            _ => 3,
        };
    }

    private static int GetHealthyDomainCount(DnsBenchmarkProfileResult result)
    {
        return result.TestResult.DomainResults.Count(domainResult => domainResult.Status is DnsTestStatus.Ok or DnsTestStatus.Slow);
    }

    private static int GetOkDomainCount(DnsBenchmarkProfileResult result)
    {
        return result.TestResult.DomainResults.Count(domainResult => domainResult.Status == DnsTestStatus.Ok);
    }

    private static double GetAverageLatencyOrMax(DnsBenchmarkProfileResult result)
    {
        return result.TestResult.AverageLatency?.TotalMilliseconds ?? double.MaxValue;
    }
}
