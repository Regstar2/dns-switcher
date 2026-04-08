namespace DnsSwitcher.Infrastructure.Windows.Agent;

public enum AgentServiceStatus
{
    NotInstalled = 0,
    Stopped = 1,
    Running = 2,
    StartPending = 3,
    StopPending = 4,
    Unknown = 5,
}
