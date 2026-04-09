using System.Windows;

namespace DnsSwitcher.Ui;

public partial class TextResultWindow : Window
{
    public TextResultWindow(string title, string content, Window? owner = null)
    {
        InitializeComponent();

        Title = title;
        Owner = owner;
        ContentTextBox.Text = content;
        Loaded += OnLoaded;
    }

    public static void ShowDialog(string title, string content, Window? owner = null)
    {
        var dialog = new TextResultWindow(title, content, owner);
        dialog.ShowDialog();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(720, Math.Max(workArea.Width - 48, 480));
        Height = Math.Min(520, Math.Max(workArea.Height - 48, 300));

        if (Owner is null)
        {
            Left = workArea.Left + Math.Max((workArea.Width - Width) / 2, 0);
            Top = workArea.Top + Math.Max((workArea.Height - Height) / 2, 0);
        }

        ContentTextBox.Select(0, 0);
        ContentTextBox.Focus();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
