using System.Runtime.Versioning;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Agent;
using DnsSwitcher.Infrastructure.Windows.Adapters;
using DnsSwitcher.Infrastructure.Windows.ConnectivityTesting;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Dns;
using DnsSwitcher.Infrastructure.Windows.DnsTesting;
using DnsSwitcher.Infrastructure.Windows.SplitDns;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsDnsSwitcherHost : IDisposable
{
    public WindowsDnsSwitcherHost(PortableAppPaths paths, ILoggerFactory loggerFactory)
    {
        Paths = paths;
        LoggerFactory = loggerFactory;

        ProfileStore = new JsonDnsProfileStore(paths, loggerFactory.CreateLogger<JsonDnsProfileStore>());
        NetworkAdapterProvider = new WindowsNetworkAdapterProvider(loggerFactory.CreateLogger<WindowsNetworkAdapterProvider>());
        NetworkAdapterService = new NetworkAdapterService(NetworkAdapterProvider);
        ProfileService = new DnsProfileService(ProfileStore);
        DnsManager = new WindowsDnsManager(NetworkAdapterService, ProfileService, loggerFactory.CreateLogger<WindowsDnsManager>());
        DnsSwitchService = new DnsSwitchService(ProfileService, DnsManager);
        DnsQueryClient = new UdpDnsQueryClient(loggerFactory.CreateLogger<UdpDnsQueryClient>());
        DnsTester = new DnsTester(ProfileService, DnsManager, DnsQueryClient, loggerFactory.CreateLogger<DnsTester>());
        DnsBenchmarkHistoryStore = new JsonDnsBenchmarkHistoryStore(paths, loggerFactory.CreateLogger<JsonDnsBenchmarkHistoryStore>());
        DnsHealthSettingsStore = new JsonDnsHealthSettingsStore(paths, loggerFactory.CreateLogger<JsonDnsHealthSettingsStore>());
        DnsHealthStateStore = new JsonDnsHealthStateStore(paths, loggerFactory.CreateLogger<JsonDnsHealthStateStore>());
        SplitDnsRulesStore = new JsonSplitDnsRulesStore(paths, loggerFactory.CreateLogger<JsonSplitDnsRulesStore>());
        SplitDnsRuleService = new SplitDnsRuleService(SplitDnsRulesStore, ProfileService);
        SplitDnsManager = new WindowsNrptSplitDnsManager(loggerFactory.CreateLogger<WindowsNrptSplitDnsManager>());
        DnsBenchmarkSelector = new DnsBenchmarkSelector();
        SiteProbeClient = new HttpSiteProbeClient(loggerFactory.CreateLogger<HttpSiteProbeClient>());
        ConnectivityTester = new ConnectivityTester(ProfileService, DnsManager, SiteProbeClient, loggerFactory.CreateLogger<ConnectivityTester>());
        AgentClient = new NamedPipeDnsAgentClient(loggerFactory.CreateLogger<NamedPipeDnsAgentClient>());
        AgentDnsSwitchService = new AgentAwareDnsSwitchService(ProfileService, DnsSwitchService, AgentClient, loggerFactory.CreateLogger<AgentAwareDnsSwitchService>());
        AgentSplitDnsService = new AgentAwareSplitDnsService(
            ProfileService,
            SplitDnsManager,
            AgentClient,
            loggerFactory.CreateLogger<AgentAwareSplitDnsService>());
        DnsHealthFailoverService = new DnsHealthFailoverService(
            ProfileService,
            DnsManager,
            DnsTester,
            AgentDnsSwitchService,
            DnsHealthSettingsStore,
            DnsHealthStateStore,
            loggerFactory.CreateLogger<DnsHealthFailoverService>());
        DirectDnsHealthFailoverService = new DnsHealthFailoverService(
            ProfileService,
            DnsManager,
            DnsTester,
            DnsSwitchService,
            DnsHealthSettingsStore,
            DnsHealthStateStore,
            loggerFactory.CreateLogger<DnsHealthFailoverService>());
        DnsBenchmarkService = new DnsBenchmarkService(
            ProfileService,
            DnsManager,
            DnsTester,
            AgentDnsSwitchService,
            DnsBenchmarkHistoryStore,
            DnsBenchmarkSelector,
            loggerFactory.CreateLogger<DnsBenchmarkService>());
        AgentServiceManager = new WindowsAgentServiceManager(loggerFactory.CreateLogger<WindowsAgentServiceManager>());
    }

    public PortableAppPaths Paths { get; }

    public ILoggerFactory LoggerFactory { get; }

    public IProfileStore ProfileStore { get; }

    public INetworkAdapterProvider NetworkAdapterProvider { get; }

    public NetworkAdapterService NetworkAdapterService { get; }

    public IDnsManager DnsManager { get; }

    public DnsProfileService ProfileService { get; }

    public DnsSwitchService DnsSwitchService { get; }

    public IDnsQueryClient DnsQueryClient { get; }

    public DnsTester DnsTester { get; }

    public IDnsBenchmarkHistoryStore DnsBenchmarkHistoryStore { get; }

    public IDnsHealthSettingsStore DnsHealthSettingsStore { get; }

    public IDnsHealthStateStore DnsHealthStateStore { get; }

    public ISplitDnsRulesStore SplitDnsRulesStore { get; }

    public SplitDnsRuleService SplitDnsRuleService { get; }

    public ISplitDnsManager SplitDnsManager { get; }

    public AgentAwareSplitDnsService AgentSplitDnsService { get; }

    public DnsHealthFailoverService DnsHealthFailoverService { get; }

    public DnsHealthFailoverService DirectDnsHealthFailoverService { get; }

    public DnsBenchmarkSelector DnsBenchmarkSelector { get; }

    public DnsBenchmarkService DnsBenchmarkService { get; }

    public ISiteProbeClient SiteProbeClient { get; }

    public ConnectivityTester ConnectivityTester { get; }

    public IDnsAgentClient AgentClient { get; }

    public AgentAwareDnsSwitchService AgentDnsSwitchService { get; }

    public IAgentServiceManager AgentServiceManager { get; }

    public void Dispose()
    {
        LoggerFactory.Dispose();
    }
}
