internal sealed class UpdateAvailableForm : Form
{
    public UpdateAvailableForm(string version, string releaseNotes, bool light, Color accent)
    {
        Text = "TeamsIO Update Available";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(680, 520);
        MinimumSize = new Size(520, 380);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = AppBrand.CreateIcon();

        var surface = light ? Color.White : Color.FromArgb(32, 32, 32);
        var secondarySurface = light ? Color.FromArgb(247, 247, 247) : Color.FromArgb(42, 42, 42);
        var primaryText = light ? Color.FromArgb(24, 24, 24) : Color.Gainsboro;
        var secondaryText = light ? Color.FromArgb(90, 90, 90) : Color.Silver;
        BackColor = surface;
        ForeColor = primaryText;

        var heading = new Label
        {
            Dock = DockStyle.Top, Height = 68, Padding = new Padding(20, 18, 20, 8),
            Text = $"TeamsIO {version} is available",
            Font = new Font("Segoe UI Semibold", 16F), ForeColor = primaryText
        };
        var notes = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = secondarySurface, ForeColor = primaryText,
            Font = new Font("Segoe UI", 10F), Text = releaseNotes,
            DetectUrls = true, ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = true
        };
        var notesPanel = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 12), BackColor = surface
        };
        notesPanel.Controls.Add(notes);

        var updateButton = new Button
        {
            Text = "Download Update", DialogResult = DialogResult.Yes, AutoSize = true,
            MinimumSize = new Size(130, 34), BackColor = accent,
            ForeColor = GetContrastingTextColor(accent), FlatStyle = FlatStyle.Flat
        };
        updateButton.FlatAppearance.BorderColor = accent;
        var cancelButton = new Button
        {
            Text = "Not Now", DialogResult = DialogResult.No, AutoSize = true,
            MinimumSize = new Size(100, 34), BackColor = secondarySurface,
            ForeColor = primaryText, FlatStyle = FlatStyle.Flat
        };
        cancelButton.FlatAppearance.BorderColor = secondaryText;
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 12, 16, 10), BackColor = surface
        };
        buttons.Controls.Add(updateButton);
        buttons.Controls.Add(cancelButton);

        Controls.Add(notesPanel);
        Controls.Add(buttons);
        Controls.Add(heading);
        AcceptButton = updateButton;
        CancelButton = cancelButton;
        BoardHelpers.Theme.ApplyTitleBarTheme(this, light);
        Shown += (_, _) => notes.SelectionStart = 0;
    }

    private static Color GetContrastingTextColor(Color background)
    {
        var brightness = ((background.R * 299) + (background.G * 587) +
            (background.B * 114)) / 1000;
        return brightness >= 150 ? Color.Black : Color.White;
    }
}
