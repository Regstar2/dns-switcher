using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class DiagnosticTextFormatterTests
{
    [Fact]
    public void BuildDnsDetails_IncludesProfileAndDomainInformation()
    {
        var result = new DnsTestResult(
            AdapterName: "Wi-Fi",
            ProfileId: "google",
            ProfileName: "Google Public DNS",
            DnsServers: ["8.8.8.8"],
            Domains: ["google.com"],
            DomainResults:
            [
                new DnsDomainTestResult(
                    Domain: "google.com",
                    Status: DnsTestStatus.Ok,
                    SuccessfulAttempts: 3,
                    TotalAttempts: 3,
                    AverageLatency: TimeSpan.FromMilliseconds(40),
                    BestLatency: TimeSpan.FromMilliseconds(35),
                    Details: "Resolved in 3/3 attempts."),
            ],
            Status: DnsTestStatus.Ok,
            AverageLatency: TimeSpan.FromMilliseconds(40),
            Details: "Status Ok.");

        var text = DiagnosticTextFormatter.BuildDnsDetails(result);

        Assert.Contains("Google Public DNS (google)", text);
        Assert.Contains("google.com: Ok", text);
        Assert.Contains("8.8.8.8", text);
    }

    [Fact]
    public void BuildSiteDetails_IncludesTlsLine_WhenTlsWasUsed()
    {
        var result = new ConnectivityTestResult(
            AdapterName: "Wi-Fi",
            ProfileId: "cloudflare",
            ProfileName: "Cloudflare",
            Urls: ["https://cloudflare.com/"],
            UrlResults:
            [
                new UrlConnectivityTestResult(
                    Url: "https://cloudflare.com/",
                    Status: ConnectivityTestStatus.Ok,
                    SuccessfulAttempts: 2,
                    TotalAttempts: 2,
                    Dns: new SiteStageResult(true, TimeSpan.FromMilliseconds(10), "Resolved 2 address(es)."),
                    Connect: new SiteStageResult(true, TimeSpan.FromMilliseconds(20), "Connected."),
                    Tls: new SiteStageResult(true, TimeSpan.FromMilliseconds(30), "TLS established using Tls13."),
                    Http: new SiteStageResult(true, TimeSpan.FromMilliseconds(60), "HTTP 301 received via HEAD."),
                    HttpStatusCode: 301,
                    HttpMethod: "HEAD",
                    AverageLatency: TimeSpan.FromMilliseconds(120),
                    BestLatency: TimeSpan.FromMilliseconds(110),
                    Details: "Site responded in 2/2 attempts."),
            ],
            Status: ConnectivityTestStatus.Ok,
            AverageLatency: TimeSpan.FromMilliseconds(120),
            Details: "Status Ok.");

        var text = DiagnosticTextFormatter.BuildSiteDetails(result);

        Assert.Contains("TLS:", text);
        Assert.Contains("HTTP 301", text);
        Assert.Contains("Cloudflare (cloudflare)", text);
    }

    [Fact]
    public void FormatLatency_ReturnsNa_ForNull()
    {
        Assert.Equal("n/a", DiagnosticTextFormatter.FormatLatency(null));
    }
}
