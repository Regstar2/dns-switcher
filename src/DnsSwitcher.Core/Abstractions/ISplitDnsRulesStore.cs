using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface ISplitDnsRulesStore
{
    Task<SplitDnsConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SplitDnsConfiguration configuration, CancellationToken cancellationToken = default);
}
