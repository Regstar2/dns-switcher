using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class FriendlyExceptionFormatter
{
    public static string ToUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            AppConfigValidationException validationException => BuildValidationMessage(validationException),
            InvalidDataException invalidDataException => BuildInvalidDataMessage(invalidDataException),
            UnauthorizedAccessException => "Access to the application files was denied. Check folder permissions and try again.",
            IOException => "A file or network operation failed. Check the log and try again.",
            DnsProfileNotFoundException => "The selected DNS profile was not found in the current configuration.",
            NetworkAdapterNotFoundException networkAdapterException => BuildAdapterNotFoundMessage(networkAdapterException),
            NetworkAdapterDisabledException => "The selected network adapter is disabled. Enable it and try again.",
            DnsAgentUnavailableException => "DnsSwitcher Agent is not available. Start the agent service or run the application as administrator.",
            DnsOperationRequiresAdminException => "Administrator rights are required to change DNS settings.",
            DnsOperationFailedException dnsOperationException => BuildDnsOperationMessage(dnsOperationException),
            _ => "The operation failed unexpectedly. Check the log for details.",
        };
    }

    private static string BuildValidationMessage(AppConfigValidationException exception)
    {
        return "profiles.json contains invalid data:" + Environment.NewLine + string.Join(
            Environment.NewLine,
            exception.Errors.Select(error => $"- {error.Path}: {error.Message} ({error.Code})"));
    }

    private static string BuildInvalidDataMessage(InvalidDataException exception)
    {
        if (exception.Message.Contains("profiles.json", StringComparison.OrdinalIgnoreCase))
        {
            return "profiles.json could not be read. Fix the file format and try again.";
        }

        if (exception.Message.Contains("tray settings", StringComparison.OrdinalIgnoreCase))
        {
            return "Tray settings could not be read. Default tray settings will be used.";
        }

        return "A required application file could not be read.";
    }

    private static string BuildAdapterNotFoundMessage(NetworkAdapterNotFoundException exception)
    {
        return exception.Message.Contains("No suitable network adapter was selected.", StringComparison.OrdinalIgnoreCase)
            ? "No active network adapter is currently available. Connect to a network and try again."
            : exception.Message;
    }

    private static string BuildDnsOperationMessage(DnsOperationFailedException exception)
    {
        return exception.Message.Contains("No static DNS profiles are configured.", StringComparison.OrdinalIgnoreCase)
            ? exception.Message
            : "Windows could not complete the DNS operation. Check the log for details and try again.";
    }
}
