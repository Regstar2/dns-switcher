using DnsSwitcher.Infrastructure.Windows;

namespace DnsSwitcher.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WindowsDnsSwitcherHost host;
    private readonly NotifyIcon notifyIcon;

    public TrayApplicationContext(WindowsDnsSwitcherHost host)
    {
        this.host = host;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Status", null, async (_, _) => await ShowStatusAsync().ConfigureAwait(true));
        menu.Items.Add("Open data folder", null, (_, _) => OpenDataFolder());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DnsSwitcher",
            ContextMenuStrip = menu,
            Visible = true,
        };

        notifyIcon.DoubleClick += async (_, _) => await ShowStatusAsync().ConfigureAwait(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task ShowStatusAsync()
    {
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        MessageBox.Show(
            $"Active profile: {configuration.ActiveProfileId ?? "<none>"}{Environment.NewLine}" +
            $"System DNS: {status.Details}{Environment.NewLine}" +
            $"Profiles: {host.Paths.ProfilesFilePath}",
            "DnsSwitcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenDataFolder()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = host.Paths.AppDirectory,
            UseShellExecute = true,
        });
    }
}
