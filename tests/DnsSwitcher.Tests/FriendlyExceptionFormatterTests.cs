using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tests;

public sealed class FriendlyExceptionFormatterTests
{
    [Fact]
    public void ToUserMessage_FormatsValidationErrors()
    {
        var exception = new AppConfigValidationException(
        [
            new ValidationError("profiles[0].name", "Name is required.", "empty-name"),
        ]);

        var message = FriendlyExceptionFormatter.ToUserMessage(exception);

        Assert.Contains("profiles.json contains invalid data", message);
        Assert.Contains("profiles[0].name", message);
    }

    [Fact]
    public void ToUserMessage_FormatsNoAdapterSelected()
    {
        var message = FriendlyExceptionFormatter.ToUserMessage(
            new NetworkAdapterNotFoundException("No suitable network adapter was selected."));

        Assert.Contains("No active network adapter is currently available", message);
    }

    [Fact]
    public void ToUserMessage_HidesTechnicalDnsCommandDetails()
    {
        var message = FriendlyExceptionFormatter.ToUserMessage(
            new DnsOperationFailedException("Failed to apply DNS. Command: netsh ..."));

        Assert.Equal("Windows could not complete the DNS operation. Check the log for details and try again.", message);
    }
}
