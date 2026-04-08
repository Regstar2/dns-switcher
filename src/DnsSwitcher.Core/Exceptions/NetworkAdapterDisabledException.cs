namespace DnsSwitcher.Core.Exceptions;

public sealed class NetworkAdapterDisabledException(string adapterName)
    : DnsSwitchException($"Network adapter '{adapterName}' is disabled.")
{
    public string AdapterName { get; } = adapterName;
}
