using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface ISplitDnsManager
{
    Task ApplyAsync(
        SplitDnsConfiguration splitDnsConfiguration,
        AppConfig appConfig,
        CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
