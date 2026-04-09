using System.Security.AccessControl;
using System.Security.Principal;
using DnsSwitcher.Infrastructure.Windows.Agent;

namespace DnsSwitcher.Tests;

public sealed class DnsAgentPipeSecurityTests
{
    [Fact]
    public void Create_IncludesAuthenticatedUsersReadWriteAccess()
    {
        var security = DnsAgentPipeSecurity.Create();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));

        var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

        var matchingRule = rules
            .Cast<AuthorizationRule>()
            .SingleOrDefault(rule =>
                Equals(rule.IdentityReference, authenticatedUsersSid)
                && rule is AccessRule accessRule
                && accessRule.AccessControlType == AccessControlType.Allow);

        Assert.NotNull(matchingRule);

        var rightsProperty = matchingRule!.GetType().GetProperty("PipeAccessRights");
        Assert.NotNull(rightsProperty);

        var rightsValue = rightsProperty!.GetValue(matchingRule);
        Assert.NotNull(rightsValue);
        Assert.Contains("ReadWrite", rightsValue!.ToString(), StringComparison.Ordinal);
    }
}
