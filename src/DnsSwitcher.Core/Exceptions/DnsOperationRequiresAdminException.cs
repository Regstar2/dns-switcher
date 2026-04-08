namespace DnsSwitcher.Core.Exceptions;

public sealed class DnsOperationRequiresAdminException()
    : DnsSwitchException("Administrator privileges are required for this operation.");
