using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Dns;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows;

public sealed class WindowsDnsSwitcherHost : IDisposable
{
    public WindowsDnsSwitcherHost(PortableAppPaths paths, ILoggerFactory loggerFactory)
    {
        Paths = paths;
        LoggerFactory = loggerFactory;

        ProfileStore = new JsonDnsProfileStore(paths, loggerFactory.CreateLogger<JsonDnsProfileStore>());
        DnsManager = new WindowsDnsManager(loggerFactory.CreateLogger<WindowsDnsManager>());
        ProfileService = new DnsProfileService(ProfileStore);
    }

    public PortableAppPaths Paths { get; }

    public ILoggerFactory LoggerFactory { get; }

    public IProfileStore ProfileStore { get; }

    public IDnsManager DnsManager { get; }

    public DnsProfileService ProfileService { get; }

    public void Dispose()
    {
        LoggerFactory.Dispose();
    }
}
