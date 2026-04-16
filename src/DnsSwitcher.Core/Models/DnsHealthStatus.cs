namespace DnsSwitcher.Core.Models;

public enum DnsHealthStatus
{
    Disabled = 0,
    Healthy = 1,
    Degraded = 2,
    Failed = 3,
    Cooldown = 4,
}
