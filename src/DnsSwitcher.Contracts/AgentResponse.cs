namespace DnsSwitcher.Contracts;

public sealed record AgentResponse(
    int Version,
    bool Success,
    AgentErrorCode ErrorCode,
    string? ErrorMessage,
    string? ErrorTarget = null)
{
    public static AgentResponse Ok()
    {
        return new AgentResponse(AgentProtocol.CurrentVersion, true, AgentErrorCode.None, null);
    }

    public static AgentResponse Fail(AgentErrorCode errorCode, string errorMessage, string? errorTarget = null)
    {
        return new AgentResponse(AgentProtocol.CurrentVersion, false, errorCode, errorMessage, errorTarget);
    }
}
