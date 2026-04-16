namespace DnsSwitcher.Contracts;

public enum AgentCommand
{
    Ping = 0,
    ApplyProfile = 1,
    ResetToDhcp = 2,
    ApplySplitDns = 3,
    ResetSplitDns = 4,
}
