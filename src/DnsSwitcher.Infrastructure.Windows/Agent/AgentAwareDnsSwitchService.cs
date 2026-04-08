using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Security;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed class AgentAwareDnsSwitchService(
    DnsProfileService profileService,
    DnsSwitchService directSwitchService,
    IDnsAgentClient agentClient)
{
    public Task<bool> IsAgentAvailableAsync(CancellationToken cancellationToken = default)
    {
        return agentClient.IsAvailableAsync(cancellationToken);
    }

    public async Task ApplyProfileAsync(
        string profileId,
        string? adapterSelection = null,
        bool allowDirectFallback = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var profile = await profileService.GetRequiredProfileAsync(profileId, cancellationToken).ConfigureAwait(false);

        if (await agentClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await agentClient.ApplyProfileAsync(profile, adapterSelection, cancellationToken).ConfigureAwait(false);
            await profileService.SetActiveProfileAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        await directSwitchService.ApplyProfileAsync(profile.Id, adapterSelection, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetToDhcpAsync(
        string? adapterSelection = null,
        bool allowDirectFallback = true,
        CancellationToken cancellationToken = default)
    {
        if (await agentClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await agentClient.ResetToDhcpAsync(adapterSelection, cancellationToken).ConfigureAwait(false);
            await profileService.ClearActiveProfileAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        await directSwitchService.ResetToDhcpAsync(adapterSelection, cancellationToken).ConfigureAwait(false);
    }
}
