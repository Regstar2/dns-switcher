using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DnsSwitcher.Infrastructure.Windows.Agent;

internal static class DnsAgentPipeSecurity
{
    public static System.IO.Pipes.PipeSecurity Create()
    {
        var security = new System.IO.Pipes.PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        return security;
    }
}
