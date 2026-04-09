namespace DnsSwitcher.Core.Models;

public sealed record DnsQueryProbeResult(
    bool Success,
    string ServerAddress,
    TimeSpan Latency,
    int AnswerCount,
    string Details);
