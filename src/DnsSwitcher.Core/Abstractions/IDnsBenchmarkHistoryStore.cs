using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Abstractions;

public interface IDnsBenchmarkHistoryStore
{
    Task<IReadOnlyList<DnsBenchmarkResult>> LoadAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(DnsBenchmarkResult result, CancellationToken cancellationToken = default);
}
