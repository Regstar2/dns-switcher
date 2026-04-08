namespace DnsSwitcher.Core.Exceptions;

public sealed class DnsAgentUnavailableException(string message)
    : DnsSwitchException(message);
