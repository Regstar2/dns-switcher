using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Security;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed class AgentAwareSplitDnsService(
    DnsProfileService profileService,
    ISplitDnsManager directManager,
    IDnsAgentClient agentClient,
    ILogger<AgentAwareSplitDnsService> logger)
{
    public async Task ApplyAsync(
        SplitDnsConfiguration configuration,
        bool allowDirectFallback = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (await agentClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await agentClient.ApplySplitDnsAsync(configuration, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Applied Split DNS using DnsSwitcher Agent.");
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        var appConfig = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        await directManager.ApplyAsync(configuration, appConfig, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Applied Split DNS using direct administrator fallback.");
    }

    public async Task ResetAsync(
        bool allowDirectFallback = true,
        CancellationToken cancellationToken = default)
    {
        if (await agentClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await agentClient.ResetSplitDnsAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Reset Split DNS using DnsSwitcher Agent.");
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        await directManager.ResetAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Reset Split DNS using direct administrator fallback.");
    }
}
