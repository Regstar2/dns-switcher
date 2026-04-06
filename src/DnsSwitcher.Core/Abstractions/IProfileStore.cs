using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IProfileStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);

    Task<DnsConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DnsConfiguration configuration, CancellationToken cancellationToken = default);
}
