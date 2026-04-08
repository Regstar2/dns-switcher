namespace DnsSwitcher.Core.Exceptions;

public sealed class NetworkAdapterNotFoundException(string message)
    : DnsSwitchException(message);
