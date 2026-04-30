using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DnsSwitcher.Contracts;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Agent;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnsSwitcher.IntegrationTests;

public sealed class NamedPipeIpcIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidRequest_ReturnsSuccessResponse()
    {
        var pipeName = CreatePipeName();
        AgentRequest? capturedRequest = null;
        using var server = new TestPipeServer(pipeName, payload =>
        {
            capturedRequest = JsonSerializer.Deserialize<AgentRequest>(payload, JsonOptions);
            return Task.FromResult(Serialize(AgentResponse.Ok()));
        });

        var serverTask = server.RunSingleConnectionAsync();
        var client = new NamedPipeDnsAgentClient(
            NullLogger<NamedPipeDnsAgentClient>.Instance,
            pipeName,
            TimeSpan.FromMilliseconds(800));

        var isAvailable = await client.IsAvailableAsync();
        await serverTask;

        Assert.True(isAvailable);
        Assert.NotNull(capturedRequest);
        Assert.Equal(AgentCommand.Ping, capturedRequest!.Command);
        Assert.Equal(AgentProtocol.CurrentVersion, capturedRequest.Version);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InvalidRequest_ReturnsStructuredError()
    {
        var pipeName = CreatePipeName();
        using var server = new TestPipeServer(pipeName, _ =>
            Task.FromResult(Serialize(AgentResponse.Fail(AgentErrorCode.InvalidRequest, "Request payload is not valid JSON."))));

        var serverTask = server.RunSingleConnectionAsync();
        var response = await SendRawRequestAsync(pipeName, "{ this is invalid json }");
        await serverTask;

        Assert.False(response.Success);
        Assert.Equal(AgentErrorCode.InvalidRequest, response.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(response.ErrorMessage));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProtocolMismatch_ReturnsExpectedRejection()
    {
        var pipeName = CreatePipeName();
        using var server = new TestPipeServer(pipeName, payload =>
        {
            var request = JsonSerializer.Deserialize<AgentRequest>(payload, JsonOptions);
            var response = request is null || request.Version != AgentProtocol.CurrentVersion
                ? AgentResponse.Fail(AgentErrorCode.ProtocolMismatch, "Unsupported agent protocol version.")
                : AgentResponse.Ok();
            return Task.FromResult(Serialize(response));
        });

        var serverTask = server.RunSingleConnectionAsync();
        var invalidVersionRequest = new AgentRequest(
            AgentProtocol.CurrentVersion + 1,
            AgentCommand.Ping,
            Profile: null,
            AdapterSelection: null);
        var response = await SendRawRequestAsync(pipeName, Serialize(invalidVersionRequest));
        await serverTask;

        Assert.False(response.Success);
        Assert.Equal(AgentErrorCode.ProtocolMismatch, response.ErrorCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AgentUnavailable_IsHandledByClient()
    {
        var client = new NamedPipeDnsAgentClient(
            NullLogger<NamedPipeDnsAgentClient>.Instance,
            pipeName: CreatePipeName(),
            operationTimeout: TimeSpan.FromMilliseconds(250));

        var isAvailable = await client.IsAvailableAsync();
        Assert.False(isAvailable);

        await Assert.ThrowsAnyAsync<Exception>(() => client.ApplyProfileAsync(new DnsProfile
        {
            Id = "tmp",
            Name = "Temporary",
            Mode = ProfileMode.Static,
            Ipv4 = ["1.1.1.1"],
        }));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RequestTimeout_DoesNotHangTestRun()
    {
        var pipeName = CreatePipeName();
        using var server = new TestPipeServer(pipeName, async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return Serialize(AgentResponse.Ok());
        });

        var serverTask = server.RunSingleConnectionAsync();
        var client = new NamedPipeDnsAgentClient(
            NullLogger<NamedPipeDnsAgentClient>.Instance,
            pipeName,
            TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.ApplyProfileAsync(new DnsProfile
        {
            Id = "tmp",
            Name = "Temporary",
            Mode = ProfileMode.Static,
            Ipv4 = ["8.8.8.8"],
        }));

        stopwatch.Stop();
        await serverTask;

        Assert.True(exception is TimeoutException or OperationCanceledException);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    private static async Task<AgentResponse> SendRawRequestAsync(string pipeName, string payload)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await client.ConnectAsync(cts.Token);

        using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        await writer.WriteLineAsync(payload);
        var responsePayload = await reader.ReadLineAsync(cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(responsePayload));
        return JsonSerializer.Deserialize<AgentResponse>(responsePayload!, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize AgentResponse.");
    }

    private static string CreatePipeName()
    {
        return $"DnsSwitcher.Test.{Guid.NewGuid():N}";
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private sealed class TestPipeServer(string pipeName, Func<string, Task<string>> responseFactory) : IDisposable
    {
        private readonly NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        public async Task RunSingleConnectionAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            await server.WaitForConnectionAsync(cts.Token);
            var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

            try
            {
                var requestPayload = await reader.ReadLineAsync(cts.Token);
                if (requestPayload is null)
                {
                    return;
                }

                var responsePayload = await responseFactory(requestPayload);
                if (!string.IsNullOrWhiteSpace(responsePayload))
                {
                    try
                    {
                        await writer.WriteLineAsync(responsePayload.AsMemory(), cts.Token);
                    }
                    catch (IOException)
                    {
                        // Client timeout tests intentionally close the connection before the server replies.
                    }
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        public void Dispose()
        {
            server.Dispose();
        }
    }
}
