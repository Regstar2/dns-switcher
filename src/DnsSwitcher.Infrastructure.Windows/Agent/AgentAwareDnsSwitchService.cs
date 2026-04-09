using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Security;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed class AgentAwareDnsSwitchService(
    DnsProfileService profileService,
    DnsSwitchService directSwitchService,
    IDnsAgentClient agentClient,
    ILogger<AgentAwareDnsSwitchService> logger)
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
            logger.LogInformation(
                "Applied DNS profile {ProfileId} using DnsSwitcher Agent. Adapter: {AdapterSelection}",
                profile.Id,
                adapterSelection ?? "<auto>");
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            logger.LogWarning(
                "Failed to apply DNS profile {ProfileId} because DnsSwitcher Agent is unavailable and direct fallback is not allowed. Adapter: {AdapterSelection}",
                profile.Id,
                adapterSelection ?? "<auto>");
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        await directSwitchService.ApplyProfileAsync(profile.Id, adapterSelection, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Applied DNS profile {ProfileId} using direct administrator fallback. Adapter: {AdapterSelection}",
            profile.Id,
            adapterSelection ?? "<auto>");
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
            logger.LogInformation(
                "Reset DNS to DHCP using DnsSwitcher Agent. Adapter: {AdapterSelection}",
                adapterSelection ?? "<auto>");
            return;
        }

        if (!allowDirectFallback || !WindowsPrivilegeHelper.IsAdministratorOrLocalSystem())
        {
            logger.LogWarning(
                "Failed to reset DNS to DHCP because DnsSwitcher Agent is unavailable and direct fallback is not allowed. Adapter: {AdapterSelection}",
                adapterSelection ?? "<auto>");
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is not available. Install and start the agent, or run the application as administrator.");
        }

        await directSwitchService.ResetToDhcpAsync(adapterSelection, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Reset DNS to DHCP using direct administrator fallback. Adapter: {AdapterSelection}",
            adapterSelection ?? "<auto>");
    }
}
