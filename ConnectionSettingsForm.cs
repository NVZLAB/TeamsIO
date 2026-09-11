using System.Diagnostics;
using System.Text.RegularExpressions;

internal sealed class ConnectionSettingsForm : Form
{
    private readonly Image _logo = AppBrand.CreateLogo();
    private readonly Icon _appIcon = AppBrand.CreateIcon();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox _tenant;
    private readonly TextBox _client;
    private readonly TextBox _groups;

    public ConnectionSettingsForm(AppConfig config)
    {
        Text = "TeamsIO setup";
        Icon = _appIcon;
        Font = new Font("Segoe UI", 10F);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 680);
        MinimumSize = new Size(580, 500);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        BackColor = Color.White;
        var firstRun = config.Validate() is not null;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var logo = new PictureBox { Image = _logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64), Margin = new Padding(0, 0, 12, 0) };
        header.Controls.Add(logo, 0, 0);
        header.SetRowSpan(logo, 2);
        header.Controls.Add(new Label { Text = "TeamsIO", Font = new Font("Segoe UI", 22F, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(24, 39, 45) }, 1, 0);
        header.Controls.Add(new Label { Text = "Connect your people. See their availability.", AutoSize = true, ForeColor = Color.DimGray }, 1, 1);
        layout.Controls.Add(header, 0, 0);

        var connectionPage = new TabPage("Connection IDs") { BackColor = Color.White, Padding = new Padding(12), AutoScroll = true };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(4) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.Controls.Add(TextLabel("Your administrator can supply these IDs. Setting this up yourself? Start with the App registration guide tab.", 40));
        _tenant = AddField(fields, "Directory (tenant) ID", config.TenantId,
            "App registrations > TeamsIO > Overview > Directory (tenant) ID");
        _client = AddField(fields, "Application (client) ID", config.ClientId,
            "Same Overview page > Application (client) ID. Do not use Object ID.");
        _groups = AddField(fields, "Group object IDs", string.Join(Environment.NewLine, config.GroupIds),
            "Groups > All groups > your group > Overview > Object ID. One per line.", multiline: true);
        fields.Controls.Add(TextLabel("No client secret is needed. IDs are saved on this PC; sign-in happens in your browser.", 36));
        connectionPage.Controls.Add(fields);

        var guidePage = new TabPage("App registration guide") { BackColor = Color.White, Padding = new Padding(10) };
        var guideLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        guideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        guideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var guide = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = Color.White, ForeColor = Color.FromArgb(24, 39, 45),
            Font = Font, DetectUrls = true, ScrollBars = RichTextBoxScrollBars.Vertical,
            Text = SetupGuide.PlainText, AccessibleName = "App registration instructions"
        };
        guide.LinkClicked += (_, e) => OpenLink(e.LinkText);
        using (var headingFont = new Font(Font, FontStyle.Bold))
        {
            foreach (Match heading in Regex.Matches(guide.Text, @"(?m)^(Set up TeamsIO[^\r\n]*|[1-5]\. [^\r\n]*|Troubleshooting|Microsoft references)$"))
            {
                guide.Select(heading.Index, heading.Length);
                guide.SelectionFont = headingFont;
            }
        }
        guide.Select(0, 0);
        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        links.Controls.Add(Link("Open Entra admin center", "https://entra.microsoft.com"));
        links.Controls.Add(Link("Microsoft desktop setup", SetupGuide.DesktopDocumentationUrl));
        guideLayout.Controls.Add(guide, 0, 0);
        guideLayout.Controls.Add(links, 0, 1);
        guidePage.Controls.Add(guideLayout);
        _tabs.TabPages.AddRange([connectionPage, guidePage]);
        _tabs.SelectedIndex = firstRun ? 1 : 0;
        layout.Controls.Add(_tabs, 0, 1);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(12, 4, 12, 4) };
        var save = new Button { AutoSize = true, Padding = new Padding(12, 4, 12, 4) };
        void UpdateButton() => save.Text = _tabs.SelectedIndex == 1 ? "Enter connection IDs" : firstRun ? "Save and continue" : "Save and restart";
        _tabs.SelectedIndexChanged += (_, _) => UpdateButton();
        UpdateButton();
        save.Click += (_, _) =>
        {
            if (_tabs.SelectedIndex == 1) { _tabs.SelectedIndex = 0; _tenant.Focus(); return; }
            var updated = new AppConfig
            {
                TenantId = _tenant.Text.Trim(), ClientId = _client.Text.Trim(),
                GroupIds = _groups.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                RefreshSeconds = Math.Clamp(config.RefreshSeconds, 5, 3600), BlockedUsers = config.BlockedUsers
            };
            try { updated.Save(); DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Settings could not be saved", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        footer.Controls.AddRange([cancel, save]);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Label TextLabel(string text, int height) => new()
    {
        Text = text, Dock = DockStyle.Top, Height = height, Margin = new Padding(0, 2, 0, 6)
    };

    private static TextBox AddField(TableLayoutPanel fields, string label, string value, string hint, bool multiline = false)
    {
        fields.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var input = new TextBox { Text = value, Dock = DockStyle.Top, Multiline = multiline,
            AccessibleName = label, Margin = new Padding(0, 0, 0, 4) };
        if (multiline) { input.Height = 86; input.ScrollBars = ScrollBars.Vertical; input.AcceptsReturn = true; }
        fields.Controls.Add(input);
        var help = TextLabel(hint, 28);
        help.ForeColor = Color.DimGray;
        fields.Controls.Add(help);
        return input;
    }

    private LinkLabel Link(string text, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 0, 20, 8) };
        link.LinkClicked += (_, _) => OpenLink(url);
        return link;
    }

    private void OpenLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Could not open your browser.\n\n" + url + "\n\n" + ex.Message, "Open help link"); }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _logo.Dispose(); _appIcon.Dispose(); }
    }
}
