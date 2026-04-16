using System.Runtime.Versioning;
using DnsSwitcher.Agent.Windows;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: SupportedOSPlatform("windows")]

var paths = PortableAppPaths.CreateDefault();
paths.EnsureDirectories();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = DnsSwitcher.Contracts.AgentProtocol.DisplayName;
});

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new FileLoggerProvider(paths.LogFilePath));
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<WindowsDnsSwitcherHost>(serviceProvider =>
    new WindowsDnsSwitcherHost(paths, serviceProvider.GetRequiredService<ILoggerFactory>()));
builder.Services.AddHostedService<DnsAgentWorker>();
builder.Services.AddHostedService<DnsHealthMonitorWorker>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
