using System.Windows;

namespace DnsSwitcher.Ui;

public partial class TextResultWindow : Window
{
    public TextResultWindow(string title, string content, Window? owner = null)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);

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
        Width = Math.Min(760, Math.Max(workArea.Width - 48, 520));
        Height = Math.Min(560, Math.Max(workArea.Height - 48, 340));

        if (Owner is null)
        {
            Left = workArea.Left + Math.Max((workArea.Width - Width) / 2, 0);
            Top = workArea.Top + Math.Max((workArea.Height - Height) / 2, 0);
        }

        ContentTextBox.Select(0, 0);
        ContentTextBox.Focus();
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ContentTextBox.Text ?? string.Empty);
    }

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        ContentTextBox.Focus();
        ContentTextBox.SelectAll();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
