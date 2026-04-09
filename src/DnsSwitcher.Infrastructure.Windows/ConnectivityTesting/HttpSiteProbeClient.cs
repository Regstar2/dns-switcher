using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.ConnectivityTesting;

public sealed class HttpSiteProbeClient(ILogger<HttpSiteProbeClient> logger) : ISiteProbeClient
{
    private const int HeaderReadLimitBytes = 64 * 1024;
    private static readonly string[] HeadFallbackStatusCodes = ["405", "501"];

    public async Task<SiteProbeResult> ProbeAsync(
        Uri url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https"))
        {
            return new SiteProbeResult(
                Url: url.ToString(),
                Success: false,
                IsBlockedIndicator: false,
                Dns: new SiteStageResult(false, null, "URL is invalid or unsupported."),
                Connect: new SiteStageResult(false, null, "Not attempted."),
                Tls: new SiteStageResult(false, null, "Not attempted."),
                Http: new SiteStageResult(false, null, "Not attempted."),
                HttpStatusCode: null,
                HttpMethod: "HEAD",
                TotalLatency: null,
                Details: "Only absolute http/https URLs are supported.");
        }

        var result = await ProbeWithMethodAsync(url, "HEAD", timeout, cancellationToken).ConfigureAwait(false);

        if (result.HttpStatusCode is 405 or 501)
        {
            var fallbackResult = await ProbeWithMethodAsync(url, "GET", timeout, cancellationToken).ConfigureAwait(false);

            if (fallbackResult.Success)
            {
                return fallbackResult with
                {
                    Details = $"HEAD returned {result.HttpStatusCode}. Fallback GET succeeded.",
                };
            }
        }

        return result;
    }

    private async Task<SiteProbeResult> ProbeWithMethodAsync(
        Uri url,
        string method,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);
        var totalStopwatch = Stopwatch.StartNew();

        var dnsResult = await ResolveDnsAsync(url, linkedCts.Token).ConfigureAwait(false);

        if (!dnsResult.Success)
        {
            totalStopwatch.Stop();
            return new SiteProbeResult(
                Url: url.ToString(),
                Success: false,
                IsBlockedIndicator: false,
                Dns: dnsResult,
                Connect: new SiteStageResult(false, null, "Not attempted."),
                Tls: new SiteStageResult(false, null, "Not attempted."),
                Http: new SiteStageResult(false, null, "Not attempted."),
                HttpStatusCode: null,
                HttpMethod: method,
                TotalLatency: totalStopwatch.Elapsed,
                Details: dnsResult.Details);
        }

        var addresses = await System.Net.Dns.GetHostAddressesAsync(url.DnsSafeHost).WaitAsync(linkedCts.Token).ConfigureAwait(false);
        var connectOutcome = await ConnectAsync(addresses, url.Port, linkedCts.Token).ConfigureAwait(false);

        if (connectOutcome.Client is null)
        {
            totalStopwatch.Stop();
            return new SiteProbeResult(
                Url: url.ToString(),
                Success: false,
                IsBlockedIndicator: true,
                Dns: dnsResult,
                Connect: connectOutcome.StageResult,
                Tls: new SiteStageResult(false, null, "Not attempted."),
                Http: new SiteStageResult(false, null, "Not attempted."),
                HttpStatusCode: null,
                HttpMethod: method,
                TotalLatency: totalStopwatch.Elapsed,
                Details: connectOutcome.StageResult.Details);
        }

        using var tcpClient = connectOutcome.Client;
        Stream transportStream = tcpClient.GetStream();
        var tlsResult = new SiteStageResult(true, null, "TLS not required.");

        if (url.Scheme == "https")
        {
            var tlsOutcome = await AuthenticateTlsAsync(url, transportStream, linkedCts.Token).ConfigureAwait(false);
            tlsResult = tlsOutcome.StageResult;

            if (tlsOutcome.Stream is null)
            {
                totalStopwatch.Stop();
                return new SiteProbeResult(
                    Url: url.ToString(),
                    Success: false,
                    IsBlockedIndicator: true,
                    Dns: dnsResult,
                    Connect: connectOutcome.StageResult,
                    Tls: tlsResult,
                    Http: new SiteStageResult(false, null, "Not attempted."),
                    HttpStatusCode: null,
                    HttpMethod: method,
                    TotalLatency: totalStopwatch.Elapsed,
                    Details: tlsResult.Details);
            }

            transportStream = tlsOutcome.Stream;
        }

        using (transportStream as IDisposable)
        {
            var httpOutcome = await SendHttpRequestAsync(url, method, transportStream, linkedCts.Token).ConfigureAwait(false);
            totalStopwatch.Stop();

            return new SiteProbeResult(
                Url: url.ToString(),
                Success: httpOutcome.StageResult.Success,
                IsBlockedIndicator: httpOutcome.HttpStatusCode == 451 || IsBlockedLike(httpOutcome.StageResult.Details),
                Dns: dnsResult,
                Connect: connectOutcome.StageResult,
                Tls: tlsResult,
                Http: httpOutcome.StageResult,
                HttpStatusCode: httpOutcome.HttpStatusCode,
                HttpMethod: method,
                TotalLatency: totalStopwatch.Elapsed,
                Details: httpOutcome.StageResult.Details);
        }
    }

    private async Task<SiteStageResult> ResolveDnsAsync(Uri url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(url.DnsSafeHost).WaitAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (addresses.Length == 0)
            {
                return new SiteStageResult(false, stopwatch.Elapsed, "DNS returned no addresses.");
            }

            return new SiteStageResult(true, stopwatch.Elapsed, $"Resolved {addresses.Length} address(es).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new SiteStageResult(false, stopwatch.Elapsed, "DNS resolution timed out.");
        }
        catch (SocketException exception)
        {
            stopwatch.Stop();
            logger.LogDebug(exception, "DNS resolution failed for {Host}.", url.DnsSafeHost);
            return new SiteStageResult(false, stopwatch.Elapsed, $"DNS resolution failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogDebug(exception, "DNS resolution failed for {Host}.", url.DnsSafeHost);
            return new SiteStageResult(false, stopwatch.Elapsed, exception.Message);
        }
    }

    private static async Task<(TcpClient? Client, SiteStageResult StageResult)> ConnectAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        string? lastError = null;

        foreach (var address in addresses)
        {
            var stopwatch = Stopwatch.StartNew();
            var tcpClient = new TcpClient(address.AddressFamily);

            try
            {
                await tcpClient.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                return (
                    tcpClient,
                    new SiteStageResult(true, stopwatch.Elapsed, $"Connected to {address}:{port}."));
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException)
            {
                stopwatch.Stop();
                tcpClient.Dispose();
                lastError = exception is SocketException socketException
                    ? $"TCP connect failed to {address}:{port}: {socketException.Message}"
                    : $"TCP connect timed out to {address}:{port}.";
            }
        }

        return (
            null,
            new SiteStageResult(false, null, lastError ?? "TCP connect failed."));
    }

    private static async Task<(Stream? Stream, SiteStageResult StageResult)> AuthenticateTlsAsync(
        Uri url,
        Stream networkStream,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

        try
        {
            await sslStream
                .AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = url.DnsSafeHost,
                        EnabledSslProtocols = SslProtocols.None,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            return (
                sslStream,
                new SiteStageResult(true, stopwatch.Elapsed, $"TLS established using {sslStream.SslProtocol}."));
        }
        catch (Exception exception) when (exception is OperationCanceledException or AuthenticationException or IOException)
        {
            stopwatch.Stop();
            sslStream.Dispose();

            return (
                null,
                new SiteStageResult(
                    false,
                    stopwatch.Elapsed,
                    exception is OperationCanceledException
                        ? "TLS handshake timed out."
                        : $"TLS handshake failed: {exception.Message}"));
        }
    }

    private static async Task<(SiteStageResult StageResult, int? HttpStatusCode)> SendHttpRequestAsync(
        Uri url,
        string method,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var requestBytes = Encoding.ASCII.GetBytes(BuildHttpRequest(url, method));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var headersText = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (string.IsNullOrWhiteSpace(headersText))
            {
                return (new SiteStageResult(false, stopwatch.Elapsed, "HTTP response headers were empty."), null);
            }

            var statusCode = ParseStatusCode(headersText);

            if (statusCode is null)
            {
                return (new SiteStageResult(false, stopwatch.Elapsed, "HTTP status code could not be parsed."), null);
            }

            var details = $"HTTP {statusCode} received via {method}.";
            return (new SiteStageResult(true, stopwatch.Elapsed, details), statusCode);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return (new SiteStageResult(false, stopwatch.Elapsed, "HTTP request timed out."), null);
        }
        catch (IOException exception)
        {
            stopwatch.Stop();
            return (new SiteStageResult(false, stopwatch.Elapsed, $"HTTP request failed: {exception.Message}"), null);
        }
    }

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var bufferStream = new MemoryStream();
        var buffer = new byte[1024];

        while (bufferStream.Length < HeaderReadLimitBytes)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            bufferStream.Write(buffer, 0, bytesRead);

            if (HasHeaderTerminator(bufferStream.GetBuffer(), (int)bufferStream.Length))
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(bufferStream.GetBuffer(), 0, (int)bufferStream.Length);
    }

    private static bool HasHeaderTerminator(byte[] buffer, int length)
    {
        if (length < 4)
        {
            return false;
        }

        for (var index = 3; index < length; index++)
        {
            if (buffer[index - 3] == '\r'
                && buffer[index - 2] == '\n'
                && buffer[index - 1] == '\r'
                && buffer[index] == '\n')
            {
                return true;
            }
        }

        return false;
    }

    private static int? ParseStatusCode(string headersText)
    {
        var firstLine = headersText
            .Split(["\r\n"], StringSplitOptions.None)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
        {
            return null;
        }

        return statusCode;
    }

    private static string BuildHttpRequest(Uri url, string method)
    {
        var hostHeader = url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
        var pathAndQuery = string.IsNullOrWhiteSpace(url.PathAndQuery) ? "/" : url.PathAndQuery;

        return
            $"{method} {pathAndQuery} HTTP/1.1\r\n" +
            $"Host: {hostHeader}\r\n" +
            "User-Agent: DnsSwitcher/0.9.1\r\n" +
            "Accept: */*\r\n" +
            "Connection: close\r\n" +
            "\r\n";
    }

    private static bool IsBlockedLike(string details)
    {
        var normalized = details.ToLowerInvariant();
        return normalized.Contains("timed out", StringComparison.Ordinal)
            || normalized.Contains("refused", StringComparison.Ordinal)
            || normalized.Contains("reset", StringComparison.Ordinal)
            || normalized.Contains("unreachable", StringComparison.Ordinal)
            || normalized.Contains("legal reasons", StringComparison.Ordinal);
    }
}
