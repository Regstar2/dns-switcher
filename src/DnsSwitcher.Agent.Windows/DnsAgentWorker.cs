using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DnsSwitcher.Contracts;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Agent.Windows;

internal sealed class DnsAgentWorker(
    WindowsDnsSwitcherHost host,
    ILogger<DnsAgentWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DnsSwitcher Agent started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var server = NamedPipeServerStreamAcl.Create(
                AgentProtocol.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: DnsAgentPipeSecurity.Create());

            try
            {
                await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                await HandleConnectionAsync(server, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "DnsSwitcher Agent pipe loop failed.");
            }
        }

        logger.LogInformation("DnsSwitcher Agent stopped.");
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };

        var requestPayload = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(requestPayload))
        {
            await WriteResponseAsync(writer, AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Request payload is empty."), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        AgentResponse response;

        try
        {
            var request = JsonSerializer.Deserialize<AgentRequest>(requestPayload, JsonOptions);

            if (request is null)
            {
                response = AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Request payload is invalid.");
            }
            else if (request.Version != AgentProtocol.CurrentVersion)
            {
                response = AgentResponse.Fail(
                    AgentErrorCode.ProtocolMismatch,
                    $"Unsupported agent protocol version '{request.Version}'.");
            }
            else
            {
                response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to parse agent request.");
            response = AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Request payload is not valid JSON.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected agent failure while handling a request.");
            response = AgentResponse.Fail(AgentErrorCode.InternalError, exception.Message);
        }

        await WriteResponseAsync(writer, response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentResponse> HandleRequestAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            switch (request.Command)
            {
                case AgentCommand.Ping:
                    return AgentResponse.Ok();

                case AgentCommand.ApplyProfile:
                    if (request.Profile is null)
                    {
                        return AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Profile payload is required.");
                    }

                    var profileValidationError = ValidateProfile(request.Profile);
                    if (profileValidationError is not null)
                    {
                        return AgentResponse.Fail(AgentErrorCode.InvalidRequest, profileValidationError);
                    }

                    await host.DnsManager.ApplyProfileAsync(request.Profile, request.AdapterSelection, cancellationToken)
                        .ConfigureAwait(false);
                    return AgentResponse.Ok();

                case AgentCommand.ResetToDhcp:
                    await host.DnsManager.ResetToDhcpAsync(request.AdapterSelection, cancellationToken)
                        .ConfigureAwait(false);
                    return AgentResponse.Ok();

                case AgentCommand.ApplySplitDns:
                    if (request.SplitDnsConfiguration is null)
                    {
                        return AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Split DNS configuration payload is required.");
                    }

                    var appConfig = await host.ProfileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
                    await host.SplitDnsManager.ApplyAsync(request.SplitDnsConfiguration, appConfig, cancellationToken)
                        .ConfigureAwait(false);
                    return AgentResponse.Ok();

                case AgentCommand.ResetSplitDns:
                    await host.SplitDnsManager.ResetAsync(cancellationToken).ConfigureAwait(false);
                    return AgentResponse.Ok();

                default:
                    return AgentResponse.Fail(AgentErrorCode.InvalidRequest, $"Unsupported agent command '{request.Command}'.");
            }
        }
        catch (DnsProfileNotFoundException exception)
        {
            return AgentResponse.Fail(AgentErrorCode.ProfileNotFound, exception.Message, exception.ProfileId);
        }
        catch (NetworkAdapterNotFoundException exception)
        {
            return AgentResponse.Fail(AgentErrorCode.AdapterNotFound, exception.Message);
        }
        catch (NetworkAdapterDisabledException exception)
        {
            return AgentResponse.Fail(AgentErrorCode.AdapterDisabled, exception.Message, exception.AdapterName);
        }
        catch (DnsOperationRequiresAdminException exception)
        {
            return AgentResponse.Fail(AgentErrorCode.RequiresAdministrator, exception.Message);
        }
        catch (DnsOperationFailedException exception)
        {
            return AgentResponse.Fail(AgentErrorCode.DnsOperationFailed, exception.Message);
        }
    }

    private static string? ValidateProfile(DnsProfile profile)
    {
        var validationConfig = new AppConfig
        {
            Version = AppConfig.CurrentVersion,
            ActiveProfileId = null,
            Profiles = [profile],
        };

        var firstError = AppConfigValidator.Validate(validationConfig).FirstOrDefault();
        return firstError?.Message;
    }

    private static async Task WriteResponseAsync(
        StreamWriter writer,
        AgentResponse response,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(response, JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}
