namespace DnsSwitcher.Core.Exceptions;

public sealed class DnsOperationRequiresAdminException()
    : DnsSwitchException("Administrator privileges are required to change DNS settings.");
