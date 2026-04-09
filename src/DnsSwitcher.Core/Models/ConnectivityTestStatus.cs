namespace DnsSwitcher.Core.Models;

public enum ConnectivityTestStatus
{
    Ok = 0,
    Slow = 1,
    Blocked = 2,
    Failed = 3,
    NotConfigured = 4,
}
