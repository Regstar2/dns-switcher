namespace DnsSwitcher.Core.Models;

[Flags]
public enum NetworkStackSupport
{
    None = 0,
    Ipv4 = 1,
    Ipv6 = 2,
}
