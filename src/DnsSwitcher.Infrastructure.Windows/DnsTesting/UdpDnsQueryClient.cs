using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.DnsTesting;

public sealed class UdpDnsQueryClient(ILogger<UdpDnsQueryClient> logger) : IDnsQueryClient
{
    private const int DnsPort = 53;
    private const ushort QueryTypeA = 1;
    private const ushort QueryTypeAaaa = 28;
    private const ushort QueryClassInternet = 1;
    private const ushort QueryFlags = 0x0100;
    private const int HeaderLength = 12;

    public async Task<DnsQueryProbeResult> QueryAsync(
        string serverAddress,
        string domain,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(serverAddress, out var serverIpAddress))
        {
            return new DnsQueryProbeResult(
                Success: false,
                ServerAddress: serverAddress,
                Latency: TimeSpan.Zero,
                AnswerCount: 0,
                Details: $"DNS server '{serverAddress}' is not a valid IP address.");
        }

        var normalizedDomain = NormalizeDomain(domain);

        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            return new DnsQueryProbeResult(
                Success: false,
                ServerAddress: serverAddress,
                Latency: TimeSpan.Zero,
                AnswerCount: 0,
                Details: "Domain is empty.");
        }

        var ipv4Result = await SendQueryAsync(serverIpAddress, normalizedDomain, QueryTypeA, timeout, cancellationToken)
            .ConfigureAwait(false);

        if (ipv4Result.Success || ipv4Result.AnswerCount > 0 || !ShouldFallbackToIpv6(ipv4Result.Details))
        {
            return ipv4Result with { ServerAddress = serverAddress };
        }

        var ipv6Result = await SendQueryAsync(serverIpAddress, normalizedDomain, QueryTypeAaaa, timeout, cancellationToken)
            .ConfigureAwait(false);

        return ipv6Result with { ServerAddress = serverAddress };
    }

    private async Task<DnsQueryProbeResult> SendQueryAsync(
        IPAddress serverIpAddress,
        string domain,
        ushort queryType,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var transactionId = (ushort)Random.Shared.Next(ushort.MinValue, ushort.MaxValue + 1);
        var payload = BuildQueryPayload(transactionId, domain, queryType);
        var endpoint = new IPEndPoint(serverIpAddress, DnsPort);
        using var client = new UdpClient(serverIpAddress.AddressFamily);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.SendAsync(payload, endpoint, linkedCts.Token).ConfigureAwait(false);
            var response = await client.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            var parseResult = ParseResponse(response.Buffer, transactionId);

            if (!parseResult.Success)
            {
                return new DnsQueryProbeResult(
                    Success: false,
                    ServerAddress: serverIpAddress.ToString(),
                    Latency: stopwatch.Elapsed,
                    AnswerCount: parseResult.AnswerCount,
                    Details: parseResult.Details,
                    AnswerAddresses: parseResult.AnswerAddresses);
            }

            return new DnsQueryProbeResult(
                Success: true,
                ServerAddress: serverIpAddress.ToString(),
                Latency: stopwatch.Elapsed,
                AnswerCount: parseResult.AnswerCount,
                Details: $"Resolved with {parseResult.AnswerCount} answer(s).",
                AnswerAddresses: parseResult.AnswerAddresses);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DnsQueryProbeResult(
                Success: false,
                ServerAddress: serverIpAddress.ToString(),
                Latency: stopwatch.Elapsed,
                AnswerCount: 0,
                Details: "DNS query timed out.");
        }
        catch (SocketException exception)
        {
            stopwatch.Stop();
            logger.LogDebug(exception, "DNS query to {ServerAddress} failed.", serverIpAddress);
            return new DnsQueryProbeResult(
                Success: false,
                ServerAddress: serverIpAddress.ToString(),
                Latency: stopwatch.Elapsed,
                AnswerCount: 0,
                Details: $"Socket error: {exception.Message}");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogDebug(exception, "DNS query to {ServerAddress} failed.", serverIpAddress);
            return new DnsQueryProbeResult(
                Success: false,
                ServerAddress: serverIpAddress.ToString(),
                Latency: stopwatch.Elapsed,
                AnswerCount: 0,
                Details: exception.Message);
        }
    }

    private static byte[] BuildQueryPayload(ushort transactionId, string domain, ushort queryType)
    {
        using var stream = new MemoryStream();

        WriteUInt16(stream, transactionId);
        WriteUInt16(stream, QueryFlags);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);

        foreach (var label in domain.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var labelBytes = Encoding.ASCII.GetBytes(label);
            stream.WriteByte((byte)labelBytes.Length);
            stream.Write(labelBytes);
        }

        stream.WriteByte(0);
        WriteUInt16(stream, queryType);
        WriteUInt16(stream, QueryClassInternet);

        return stream.ToArray();
    }

    private static (bool Success, int AnswerCount, string Details, IReadOnlyList<string> AnswerAddresses) ParseResponse(
        byte[] buffer,
        ushort expectedTransactionId)
    {
        if (buffer.Length < HeaderLength)
        {
            return (false, 0, "DNS response is too short.", []);
        }

        var transactionId = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(0, 2));

        if (transactionId != expectedTransactionId)
        {
            return (false, 0, "DNS response transaction id does not match the request.", []);
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(2, 2));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(6, 2));
        var responseCode = flags & 0x000F;

        if ((flags & 0x8000) == 0)
        {
            return (false, answerCount, "DNS response flag is missing.", []);
        }

        if (responseCode != 0)
        {
            return (false, answerCount, $"DNS server returned {GetResponseCodeName(responseCode)}.", []);
        }

        if (answerCount == 0)
        {
            return (false, 0, "DNS server returned no answers.", []);
        }

        return (true, answerCount, string.Empty, ParseAnswerAddresses(buffer, answerCount));
    }

    private static IReadOnlyList<string> ParseAnswerAddresses(byte[] buffer, int answerCount)
    {
        var offset = HeaderLength;

        if (!TrySkipName(buffer, ref offset))
        {
            return [];
        }

        if (!TrySkipBytes(buffer, ref offset, 4))
        {
            return [];
        }

        var addresses = new List<string>();

        for (var index = 0; index < answerCount; index++)
        {
            if (!TrySkipName(buffer, ref offset) || !TryReadUInt16(buffer, ref offset, out var type))
            {
                break;
            }

            if (!TryReadUInt16(buffer, ref offset, out _)
                || !TrySkipBytes(buffer, ref offset, 4)
                || !TryReadUInt16(buffer, ref offset, out var dataLength))
            {
                break;
            }

            if (offset + dataLength > buffer.Length)
            {
                break;
            }

            if (type == QueryTypeA && dataLength == 4)
            {
                addresses.Add(new IPAddress(buffer.AsSpan(offset, dataLength)).ToString());
            }
            else if (type == QueryTypeAaaa && dataLength == 16)
            {
                addresses.Add(new IPAddress(buffer.AsSpan(offset, dataLength)).ToString());
            }

            offset += dataLength;
        }

        return addresses
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TrySkipName(byte[] buffer, ref int offset)
    {
        while (offset < buffer.Length)
        {
            var length = buffer[offset];

            if ((length & 0xC0) == 0xC0)
            {
                return TrySkipBytes(buffer, ref offset, 2);
            }

            offset++;

            if (length == 0)
            {
                return true;
            }

            if (!TrySkipBytes(buffer, ref offset, length))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryReadUInt16(byte[] buffer, ref int offset, out ushort value)
    {
        value = 0;

        if (offset + 2 > buffer.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset, 2));
        offset += 2;
        return true;
    }

    private static bool TrySkipBytes(byte[] buffer, ref int offset, int length)
    {
        if (offset + length > buffer.Length)
        {
            return false;
        }

        offset += length;
        return true;
    }

    private static bool ShouldFallbackToIpv6(string details)
    {
        return string.Equals(details, "DNS server returned no answers.", StringComparison.Ordinal);
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimEnd('.');
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static string GetResponseCodeName(int responseCode)
    {
        return responseCode switch
        {
            1 => "FormatError",
            2 => "ServerFailure",
            3 => "NameError",
            4 => "NotImplemented",
            5 => "Refused",
            _ => $"ResponseCode({responseCode})",
        };
    }
}
