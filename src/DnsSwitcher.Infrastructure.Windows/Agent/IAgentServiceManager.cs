namespace DnsSwitcher.Infrastructure.Windows.Agent;

public interface IAgentServiceManager
{
    Task InstallAsync(string? agentExecutablePath = null, CancellationToken cancellationToken = default);

    Task UninstallAsync(CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<AgentServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
