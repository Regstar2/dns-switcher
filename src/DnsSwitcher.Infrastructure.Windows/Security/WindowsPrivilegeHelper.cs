using System.Security.Principal;

namespace DnsSwitcher.Infrastructure.Windows.Security;

internal static class WindowsPrivilegeHelper
{
    public static bool IsAdministratorOrLocalSystem()
    {
        using var identity = WindowsIdentity.GetCurrent();

        if (identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true)
        {
            return true;
        }

        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
