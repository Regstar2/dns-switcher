namespace DnsSwitcher.Infrastructure.Windows.Agent;

public sealed record AgentServiceInfo(
    AgentServiceStatus Status,
    string? ServiceBinaryPath,
    string ExpectedBinaryPath,
    bool PointsToExpectedPath)
{
    public bool IsInstalled => Status != AgentServiceStatus.NotInstalled;

    public bool IsStalePath => IsInstalled && !PointsToExpectedPath;
}
