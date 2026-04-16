using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class SplitDnsRuleServiceTests
{
    [Fact]
    public async Task AddRuleAsync_NormalizesWildcardAndMatchesSubdomain()
    {
        var service = CreateService(out _, out _);

        var rule = await service.AddRuleAsync("*.example.com", "cloudflare");
        var match = await service.TestMatchAsync("api.example.com");

        Assert.Equal(".example.com", rule.Namespace);
        Assert.True(match.Matched);
        Assert.Equal(rule.Id, match.Rule?.Id);
    }

    [Fact]
    public async Task SaveConfigurationAsync_RejectsConflictingEnabledRules()
    {
        var service = CreateService(out _, out _);
        var configuration = new SplitDnsConfiguration
        {
            Enabled = true,
            Rules =
            [
                new SplitDnsRule { Id = "one", Namespace = ".example.com", ProfileId = "cloudflare", Enabled = true },
                new SplitDnsRule { Id = "two", Namespace = "*.example.com", ProfileId = "google", Enabled = true },
            ],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveConfigurationAsync(configuration));
    }

    [Fact]
    public async Task SaveConfigurationAsync_RejectsDuplicateRuleIds()
    {
        var service = CreateService(out _, out _);
        var configuration = new SplitDnsConfiguration
        {
            Rules =
            [
                new SplitDnsRule { Id = "work", Namespace = "one.example.com", ProfileId = "cloudflare" },
                new SplitDnsRule { Id = "WORK", Namespace = "two.example.com", ProfileId = "google" },
            ],
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveConfigurationAsync(configuration));

        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("bad domain.example.com")]
    [InlineData("bad..example.com")]
    [InlineData("-bad.example.com")]
    [InlineData("bad-.example.com")]
    [InlineData("bad*.example.com")]
    public async Task SaveConfigurationAsync_RejectsInvalidNamespaces(string @namespace)
    {
        var service = CreateService(out _, out _);
        var configuration = new SplitDnsConfiguration
        {
            Rules =
            [
                new SplitDnsRule { Id = "bad", Namespace = @namespace, ProfileId = "cloudflare" },
            ],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveConfigurationAsync(configuration));
    }

    [Fact]
    public async Task SetRuleEnabledAsync_DisablesRuleMatching()
    {
        var service = CreateService(out _, out _);
        var rule = await service.AddRuleAsync("example.com", "cloudflare");

        await service.SetRuleEnabledAsync(rule.Id, enabled: false);
        var match = await service.TestMatchAsync("example.com");

        Assert.False(match.Matched);
    }

    [Fact]
    public async Task UpdateRuleAsync_ChangesNamespaceAndProfile()
    {
        var service = CreateService(out _, out _);
        var rule = await service.AddRuleAsync("old.example.com", "cloudflare");

        var updated = await service.UpdateRuleAsync(rule.Id, "new.example.com", "google");

        Assert.Equal("new.example.com", updated.Namespace);
        Assert.Equal("google", updated.ProfileId);
    }

    private static SplitDnsRuleService CreateService(
        out InMemorySplitDnsRulesStore rulesStore,
        out InMemoryProfileStore profileStore)
    {
        rulesStore = new InMemorySplitDnsRulesStore();
        profileStore = new InMemoryProfileStore(AppConfig.CreateDefault());
        return new SplitDnsRuleService(rulesStore, new DnsProfileService(profileStore));
    }

    private sealed class InMemorySplitDnsRulesStore : ISplitDnsRulesStore
    {
        private SplitDnsConfiguration configuration = SplitDnsConfiguration.Default;

        public Task<SplitDnsConfiguration> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(configuration);
        }

        public Task SaveAsync(SplitDnsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            this.configuration = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProfileStore(AppConfig configuration) : IProfileStore
    {
        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(configuration);

        public Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
