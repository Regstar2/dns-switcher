using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Tray;

internal sealed class AutomaticUpdateMonitor : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly WindowsDnsSwitcherHost host;
    private readonly ILogger<AutomaticUpdateMonitor> logger;
    private readonly JsonAppPreferencesStore preferencesStore;
    private readonly JsonUpdateStateStore stateStore;
    private readonly System.Windows.Forms.Timer timer;
    private bool isChecking;

    public AutomaticUpdateMonitor(WindowsDnsSwitcherHost host)
    {
        this.host = host;
        logger = host.LoggerFactory.CreateLogger<AutomaticUpdateMonitor>();
        preferencesStore = new JsonAppPreferencesStore(host.Paths, host.LoggerFactory.CreateLogger<JsonAppPreferencesStore>());
        stateStore = new JsonUpdateStateStore(host.Paths, host.LoggerFactory.CreateLogger<JsonUpdateStateStore>());
        timer = new System.Windows.Forms.Timer
        {
            Interval = 2000,
        };
        timer.Tick += OnTimerTick;
    }

    public void Start() => timer.Start();

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= OnTimerTick;
        timer.Dispose();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        timer.Stop();
        timer.Interval = checked((int)PollInterval.TotalMilliseconds);

        try
        {
            await CheckOnceAsync().ConfigureAwait(true);
        }
        finally
        {
            timer.Start();
        }
    }

    private async Task CheckOnceAsync()
    {
        if (isChecking)
        {
            return;
        }

        isChecking = true;
        try
        {
            var preferences = await preferencesStore.LoadAsync().ConfigureAwait(true);
            if (!preferences.AutomaticUpdateChecksEnabled)
            {
                return;
            }

            var state = await stateStore.LoadAsync().ConfigureAwait(true);
            var now = DateTimeOffset.UtcNow;
            if (state.LastCheckedUtc is not null && now - state.LastCheckedUtc.Value < CheckInterval)
            {
                return;
            }

            var result = await host.UpdateService
                .CheckForUpdatesAsync(host.ApplicationMetadata.Version, UpdateChannel.Stable)
                .ConfigureAwait(true);

            var updatedState = state with { LastCheckedUtc = now };
            if (result.Status == UpdateCheckStatus.UpdateAvailable
                && result.Update is not null
                && !string.Equals(state.LastNotifiedVersion, result.Update.Version.ToString(), StringComparison.Ordinal))
            {
                var localizer = new AppLocalizer(preferences.Language);
                TrayDialogs.ShowInformation(
                    localizer.GetUpdateText("UpdateDialogTitle"),
                    localizer.FormatUpdateText("UpdateTrayAvailableFormat", result.Update.Version),
                    preferences.Theme);
                updatedState = updatedState with { LastNotifiedVersion = result.Update.Version.ToString() };
            }

            await stateStore.SaveAsync(updatedState).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Automatic update check failed; tray startup and DNS functionality are unaffected.");
        }
        finally
        {
            isChecking = false;
        }
    }
}
