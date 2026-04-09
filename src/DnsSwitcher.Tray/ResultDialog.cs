namespace DnsSwitcher.Tray;

internal sealed class ResultDialog : Form
{
    private ResultDialog(string title, string message)
    {
        Text = title;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = new Size(480, 300);

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var width = Math.Min(720, Math.Max(workingArea.Width - 48, 480));
        var height = Math.Min(520, Math.Max(workingArea.Height - 48, 300));

        Size = new Size(width, height);
        Location = new Point(
            workingArea.Left + Math.Max((workingArea.Width - width) / 2, 0),
            workingArea.Top + Math.Max((workingArea.Height - height) / 2, 0));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.5f),
            Text = message,
            HideSelection = false,
            ShortcutsEnabled = true,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(80, 30),
        };

        buttonPanel.Controls.Add(okButton);
        layout.Controls.Add(textBox, 0, 0);
        layout.Controls.Add(buttonPanel, 0, 1);

        AcceptButton = okButton;
        CancelButton = okButton;
        Controls.Add(layout);
    }

    public static void ShowDialog(string title, string message)
    {
        using var dialog = new ResultDialog(title, message);
        dialog.ShowDialog();
    }
}
