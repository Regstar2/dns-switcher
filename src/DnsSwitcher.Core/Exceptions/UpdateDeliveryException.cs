using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Exceptions;

public sealed class UpdateDeliveryException : Exception
{
    public UpdateDeliveryException(UpdateFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public UpdateDeliveryException(UpdateFailureKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public UpdateFailureKind Kind { get; }
}
