using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsHealthSettingsStore
{
    Task<DnsHealthSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DnsHealthSettings settings, CancellationToken cancellationToken = default);
}
