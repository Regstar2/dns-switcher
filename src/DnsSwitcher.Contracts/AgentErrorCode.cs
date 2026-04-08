namespace DnsSwitcher.Contracts;

public enum AgentErrorCode
{
    None = 0,
    InvalidRequest = 1,
    ProtocolMismatch = 2,
    ProfileNotFound = 3,
    AdapterNotFound = 4,
    AdapterDisabled = 5,
    RequiresAdministrator = 6,
    DnsOperationFailed = 7,
    InternalError = 8,
}
