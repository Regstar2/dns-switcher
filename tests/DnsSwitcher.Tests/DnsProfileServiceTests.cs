using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class DnsProfileServiceTests
{
    [Fact]
    public async Task SaveProfileAsync_ReplacesPreviousProfileId_AndKeepsActiveProfile()
    {
        var store = new InMemoryProfileStore(AppConfig.CreateDefault() with
        {
            ActiveProfileId = "google",
        });
        var service = new DnsProfileService(store);

        await service.SaveProfileAsync(
            new DnsProfile
            {
                Id = "google-fast",
                Name = "Google Fast",
                Mode = ProfileMode.Static,
                Ipv4 = ["8.8.8.8", "8.8.4.4"],
                TestDomains = ["google.com"],
            },
            previousProfileId: "google");

        var configuration = await store.LoadAsync();

        Assert.DoesNotContain(configuration.Profiles, profile => profile.Id == "google");
        Assert.Contains(configuration.Profiles, profile => profile.Id == "google-fast");
        Assert.Equal("google-fast", configuration.ActiveProfileId);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesProfile_AndClearsActiveProfile()
    {
        var store = new InMemoryProfileStore(AppConfig.CreateDefault() with
        {
            ActiveProfileId = "cloudflare",
        });
        var service = new DnsProfileService(store);

        await service.DeleteProfileAsync("cloudflare");

        var configuration = await store.LoadAsync();

        Assert.DoesNotContain(configuration.Profiles, profile => profile.Id == "cloudflare");
        Assert.Null(configuration.ActiveProfileId);
    }

    [Fact]
    public async Task ImportProfilesAsync_ReplacesExistingProfilesById_AndAppendsNewProfiles()
    {
        var store = new InMemoryProfileStore(AppConfig.CreateDefault());
        var service = new DnsProfileService(store);

        var importedCount = await service.ImportProfilesAsync(
        [
            new DnsProfile
            {
                Id = "google",
                Name = "Google Updated",
                Mode = ProfileMode.Static,
                Ipv4 = ["8.8.8.8"],
                TestDomains = ["google.com"],
            },
            new DnsProfile
            {
                Id = "quad9",
                Name = "Quad9",
                Mode = ProfileMode.Static,
                Ipv4 = ["9.9.9.9"],
                TestDomains = ["quad9.net"],
            },
        ]);

        var configuration = await store.LoadAsync();

        Assert.Equal(2, importedCount);
        Assert.Contains(configuration.Profiles, profile => profile.Id == "google" && profile.Name == "Google Updated");
        Assert.Contains(configuration.Profiles, profile => profile.Id == "quad9");
    }

    [Fact]
    public async Task DeleteProfileAsync_Throws_WhenProfileDoesNotExist()
    {
        var service = new DnsProfileService(new InMemoryProfileStore(AppConfig.CreateDefault()));

        await Assert.ThrowsAsync<DnsProfileNotFoundException>(() => service.DeleteProfileAsync("missing"));
    }

    private sealed class InMemoryProfileStore(AppConfig configuration) : IProfileStore
    {
        private AppConfig configuration = configuration;

        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(configuration);
        }

        public Task SaveAsync(AppConfig configuration, CancellationToken cancellationToken = default)
        {
            this.configuration = configuration;
            return Task.CompletedTask;
        }
    }
}
