namespace DnsSwitcher.Core.Models;

public sealed record SplitDnsRuleMatch(
    string Domain,
    bool Matched,
    SplitDnsRule? Rule,
    string Details);
