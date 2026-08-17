using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DnsSwitcher.Ui.Controls;

public sealed class TransientStatusTextBlock : TextBlock
{
    private static readonly TimeSpan DismissDelay = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer dismissTimer;

    public TransientStatusTextBlock()
    {
        dismissTimer = new DispatcherTimer { Interval = DismissDelay };
        dismissTimer.Tick += OnDismissTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == TextProperty && IsLoaded)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(RefreshNotificationState));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshNotificationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        dismissTimer.Stop();
    }

    private void RefreshNotificationState()
    {
        var container = FindAncestorBorder();
        if (container is null || string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        container.Visibility = Visibility.Visible;
        dismissTimer.Stop();

        var errorBrush = TryFindResource("ErrorStatusBrush") as Brush;
        if (errorBrush is not null && ReferenceEquals(container.Background, errorBrush))
        {
            return;
        }

        dismissTimer.Start();
    }

    private Border? FindAncestorBorder()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is Border border)
            {
                return border;
            }
        }

        return null;
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        dismissTimer.Stop();
        var container = FindAncestorBorder();
        if (container is not null)
        {
            container.Visibility = Visibility.Collapsed;
        }
    }
}
