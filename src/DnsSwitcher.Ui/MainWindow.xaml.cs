using System.Windows;

namespace DnsSwitcher.Ui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var configuration = await App.Host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var paths = App.Host.Paths;

        PathsTextBox.Text =
            $"Data: {paths.AppDirectory}{Environment.NewLine}" +
            $"Profiles: {paths.ProfilesFilePath}{Environment.NewLine}" +
            $"Log: {paths.LogFilePath}";

        ProfilesListBox.Items.Clear();

        foreach (var profile in configuration.Profiles)
        {
            var activeMarker = string.Equals(profile.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase)
                ? "* "
                : string.Empty;

            ProfilesListBox.Items.Add($"{activeMarker}{profile.Name} ({profile.Id}) - IPv4: {string.Join(", ", profile.Ipv4)}");
        }
    }
}
