using DnsSwitcher.Infrastructure.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Agent.Windows;

internal sealed class DnsHealthMonitorWorker(
    WindowsDnsSwitcherHost host,
    ILogger<DnsHealthMonitorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan DisabledPollInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DNS health monitor worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DisabledPollInterval;

            try
            {
                var settings = await host.DirectDnsHealthFailoverService.GetSettingsAsync(stoppingToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Clamp(settings.MonitorIntervalSeconds, 15, 86_400));

                if (settings.Enabled)
                {
                    var result = await host.DirectDnsHealthFailoverService.EvaluateAsync(adapterIdOrName: null, stoppingToken)
                        .ConfigureAwait(false);

                    logger.LogInformation(
                        "DNS health monitor tick completed. Status: {Status}. Switched: {Switched}. Details: {Details}",
                        result.Status,
                        result.SwitchedProfile,
                        result.Details);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "DNS health monitor tick failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("DNS health monitor worker stopped.");
    }
}
