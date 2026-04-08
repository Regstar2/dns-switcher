namespace DnsSwitcher.Core.Exceptions;

public abstract class DnsSwitchException(string message, Exception? innerException = null)
    : Exception(message, innerException);
