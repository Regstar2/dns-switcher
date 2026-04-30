using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DnsSwitcher.Contracts;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed class NamedPipeDnsAgentClient : IDnsAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<NamedPipeDnsAgentClient> logger;
    private readonly string pipeName;
    private readonly TimeSpan operationTimeout;

    public NamedPipeDnsAgentClient(
        ILogger<NamedPipeDnsAgentClient> logger,
        string? pipeName = null,
        TimeSpan? operationTimeout = null)
    {
        this.logger = logger;
        this.pipeName = string.IsNullOrWhiteSpace(pipeName) ? AgentProtocol.PipeName : pipeName;
        this.operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(2);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendAsync(
                new AgentRequest(
                    AgentProtocol.CurrentVersion,
                    AgentCommand.Ping,
                    Profile: null,
                    AdapterSelection: null),
                cancellationToken).ConfigureAwait(false);

            return response.Success;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "DnsSwitcher Agent pipe is not accessible for the current user.");
            return false;
        }
        catch (DnsAgentUnavailableException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task ApplyProfileAsync(
        DnsProfile profile,
        string? adapterSelection = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var response = await SendAsync(
            new AgentRequest(
                AgentProtocol.CurrentVersion,
                AgentCommand.ApplyProfile,
                profile,
                adapterSelection),
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(response);
    }

    public async Task ResetToDhcpAsync(
        string? adapterSelection = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            new AgentRequest(
                AgentProtocol.CurrentVersion,
                AgentCommand.ResetToDhcp,
                Profile: null,
                adapterSelection),
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(response);
    }

    public async Task ApplySplitDnsAsync(
        SplitDnsConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var response = await SendAsync(
            new AgentRequest(
                AgentProtocol.CurrentVersion,
                AgentCommand.ApplySplitDns,
                Profile: null,
                AdapterSelection: null,
                SplitDnsConfiguration: configuration),
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(response);
    }

    public async Task ResetSplitDnsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            new AgentRequest(
                AgentProtocol.CurrentVersion,
                AgentCommand.ResetSplitDns,
                Profile: null,
                AdapterSelection: null),
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(response);
    }

    private async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(operationTimeout);

            await client.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            var payload = JsonSerializer.Serialize(request, JsonOptions);
            await writer.WriteLineAsync(payload).ConfigureAwait(false);

            var responsePayload = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(responsePayload))
            {
                throw new DnsOperationFailedException("DnsSwitcher Agent returned an empty response.");
            }

            var response = JsonSerializer.Deserialize<AgentResponse>(responsePayload, JsonOptions)
                ?? throw new DnsOperationFailedException("DnsSwitcher Agent returned an invalid response.");

            return response;
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "DnsSwitcher Agent pipe denied access for the current user.");
            throw new DnsAgentUnavailableException(
                "DnsSwitcher Agent is running but is not accessible for the current user. Reinstall or restart the agent service with the current version.");
        }
    }

    private void ThrowIfFailed(AgentResponse response)
    {
        if (response.Success)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(response.ErrorMessage)
            ? "DnsSwitcher Agent operation failed."
            : response.ErrorMessage;

        logger.LogWarning("DnsSwitcher Agent request failed with {ErrorCode}: {Message}", response.ErrorCode, message);

        throw response.ErrorCode switch
        {
            AgentErrorCode.ProfileNotFound => new DnsProfileNotFoundException(response.ErrorTarget ?? "unknown"),
            AgentErrorCode.AdapterNotFound => new NetworkAdapterNotFoundException(message),
            AgentErrorCode.AdapterDisabled => new NetworkAdapterDisabledException(response.ErrorTarget ?? "unknown"),
            AgentErrorCode.RequiresAdministrator => new DnsOperationRequiresAdminException(),
            AgentErrorCode.ProtocolMismatch => new DnsOperationFailedException(message),
            AgentErrorCode.InvalidRequest => new DnsOperationFailedException(message),
            AgentErrorCode.DnsOperationFailed => new DnsOperationFailedException(message),
            AgentErrorCode.InternalError => new DnsOperationFailedException(message),
            _ => new DnsOperationFailedException(message),
        };
    }
}
