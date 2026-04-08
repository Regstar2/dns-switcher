namespace DnsSwitcher.Core.Exceptions;

public sealed class DnsOperationFailedException(string message, Exception? innerException = null)
    : DnsSwitchException(message, innerException);
