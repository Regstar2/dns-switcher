namespace DnsSwitcher.Core.Models;

public enum DnsHealthFailureAction
{
    NotifyOnly = 0,
    SwitchToNextProfile = 1,
    SwitchToFallbackProfile = 2,
}
