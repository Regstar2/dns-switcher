namespace DnsSwitcher.Core.Exceptions;

public sealed class DnsProfileNotFoundException(string profileId)
    : DnsSwitchException($"DNS profile '{profileId}' was not found.")
{
    public string ProfileId { get; } = profileId;
}
