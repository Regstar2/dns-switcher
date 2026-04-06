using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IProfileStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);

    Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default);
}
