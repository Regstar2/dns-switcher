using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsQueryClient
{
    Task<DnsQueryProbeResult> QueryAsync(
        string serverAddress,
        string domain,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
