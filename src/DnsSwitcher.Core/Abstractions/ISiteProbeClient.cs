using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface ISiteProbeClient
{
    Task<SiteProbeResult> ProbeAsync(
        Uri url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
