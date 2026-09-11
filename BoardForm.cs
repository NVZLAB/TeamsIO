using System.Diagnostics;
using System.Runtime.InteropServices;
using static BoardHelpers;
using Presence = Microsoft.Graph.Models.Presence;

internal sealed class BoardForm : Form
{
    private readonly TeamsIoService _service;
    private readonly AppConfig _config;
    private readonly DataGridView _grid;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _identityLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly UpdateChecker _updateChecker;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly UserPreferences _preferences = UserPreferences.Load();
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly ToolStripMenuItem _autoThemeItem = new("Auto (Windows)");
    private readonly ToolStripMenuItem _lightThemeItem = new("Light");
    private readonly ToolStripMenuItem _darkThemeItem = new("Dark");
    private readonly ToolStripMenuItem _copyRowItem = new("Copy");
    private Color _primaryText;
    private Color _secondaryText;
    private Color _surface;
    private Color _alternateSurface;
    private Color _accentColor;
    private bool _hasLoaded;
    private bool _hasAutoSized;
    private int _contextRowIndex = -1;
    private ThemeMode _themeMode = ThemeMode.Auto;

    private enum ThemeMode
    {
        Auto,
        Light,
        Dark
    }

    public BoardForm(TeamsIoService service, AppConfig config)
    {
        _service = service;
        _config = config;
        _updateChecker = new UpdateChecker();


        Text = $"TeamsIO · v{AppInfo.CurrentVersion}";
        MinimumSize = new Size(640, 420);
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        RestoreWindowPlacement();
        Icon = AppBrand.CreateIcon();

        _grid = CreateGrid();
        _statusLabel = new ToolStripStatusLabel("Ready");
        _identityLabel = new ToolStripStatusLabel(service.SignedInAs)
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight
        };
        _statusStrip = new StatusStrip
        {
            SizingGrip = false,
            Items = { _statusLabel, _identityLabel }
        };

        _alwaysOnTopItem = new ToolStripMenuItem("Always on Top")
        {
            CheckOnClick = true,
            Checked = _preferences.AlwaysOnTop
        };
        TopMost = _preferences.AlwaysOnTop;

        Controls.Add(_grid);
        Controls.Add(_statusStrip);
        InitializeContextMenu();
        SetThemeMode(Enum.TryParse<ThemeMode>(
            _preferences.Theme,
            ignoreCase: true,
            out var savedTheme)
                ? savedTheme
                : ThemeMode.Auto);
        Microsoft.Win32.SystemEvents.UserPreferenceChanged +=
            OnWindowsUserPreferenceChanged;

        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(5, config.RefreshSeconds) * 1000
        };
        _timer.Tick += async (_, _) => await RefreshBoardAsync();

        Shown += async (_, _) =>
        {
            if (_hasLoaded)
                return;

            _hasLoaded = true;
            await RefreshBoardAsync();
            if (!IsDisposed)
                _timer.Start();
        };
        FormClosing += (_, _) =>
        {
            _timer.Stop();
            _lifetimeCancellation.Cancel();
            SavePreferences();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
            _lifetimeCancellation.Cancel();
            _updateChecker.Dispose();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -=
                OnWindowsUserPreferenceChanged;
        }

        base.Dispose(disposing);
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(32, 32, 32),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            Dock = DockStyle.Fill,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 34,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Activity",
            HeaderText = "Activity",
            FillWeight = 25,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            FillWeight = 41,
            SortMode = DataGridViewColumnSortMode.Automatic
        });

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex != grid.Columns["Activity"]?.Index)
                return;

            if (grid.Rows[e.RowIndex].Tag is BoardRowData { Presence: { } presence } &&
                e.CellStyle is DataGridViewCellStyle style)
            {
                style.ForeColor = GetPresenceColorWinForms(presence.Activity);
            }
        };

        return grid;
    }

    private async Task RefreshBoardAsync()
    {
        if (WindowState == FormWindowState.Minimized ||
            !await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            SetRefreshState(true, "Refreshing…");
            var data = await _service.GetBoardAsync(
                _config,
                _lifetimeCancellation.Token);
            var sortedColumn = _grid.SortedColumn;
            var sortDirection = _grid.SortOrder;

            _grid.Rows.Clear();

            foreach (var (user, presence) in data)
            {
                var activity = presence?.Activity ?? "Unavailable";
                var status = StripHtml(presence?.StatusMessage?.Message?.Content);
                var rowIndex = _grid.Rows.Add(
                    user.DisplayName ?? "Unknown",
                    $"●  {activity}",
                    status);
                _grid.Rows[rowIndex].Tag = new BoardRowData(user, presence);
            }

            if (sortedColumn is not null && sortDirection != SortOrder.None)
            {
                _grid.Sort(
                    sortedColumn,
                    sortDirection == SortOrder.Ascending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending);
            }
            else if (!string.IsNullOrWhiteSpace(_preferences.SortColumn) &&
                _grid.Columns[_preferences.SortColumn] is DataGridViewColumn savedColumn)
            {
                _grid.Sort(
                    savedColumn,
                    _preferences.SortDescending
                        ? System.ComponentModel.ListSortDirection.Descending
                        : System.ComponentModel.ListSortDirection.Ascending);
            }

            // DataGridView selects its first cell when rows are added. Start with
            // no active selection so the Name cell is not highlighted by default.
            _grid.ClearSelection();
            _grid.CurrentCell = null;

            if (!_hasAutoSized)
            {
                _hasAutoSized = true;
                FitWindowToRows();
            }

            _statusLabel.Text =
                $"{data.Count} people · Updated {DateTime.Now:t} · Every {_config.RefreshSeconds}s";
        }
        catch (HttpRequestException ex)
        {
            ShowRefreshError("Network unavailable", ex);
        }
        catch (OperationCanceledException)
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
                _statusLabel.Text = "Refresh canceled";
        }
        catch (Exception ex)
        {
            ShowRefreshError("Unable to refresh", ex);
        }
        finally
        {
            if (!IsDisposed)
                SetRefreshState(false);
            _refreshLock.Release();
        }
    }

    private void SetRefreshState(bool refreshing, string? message = null)
    {
        UseWaitCursor = refreshing;
        if (message is not null)
            _statusLabel.Text = message;
    }

    private void ShowRefreshError(string message, Exception exception)
    {
        Debug.WriteLine(exception);
        AppLog.Error(message, exception);
        _statusLabel.Text = $"{message} · {DateTime.Now:t}";
    }

    private void ApplyTheme(bool light)
    {
        _accentColor = Theme.GetAccentColor();

        if (light)
        {
            _surface = Color.White;
            _alternateSurface = Color.FromArgb(247, 247, 247);
            _primaryText = Color.FromArgb(24, 24, 24);
            _secondaryText = Color.FromArgb(90, 90, 90);
        }
        else
        {
            _surface = Color.FromArgb(32, 32, 32);
            _alternateSurface = Color.FromArgb(39, 39, 39);
            _primaryText = Color.Gainsboro;
            _secondaryText = Color.Silver;
        }

        BackColor = _surface;
        _grid.BackgroundColor = _surface;
        _grid.GridColor = light ? Color.FromArgb(225, 225, 225) : Color.FromArgb(58, 58, 58);
        _grid.DefaultCellStyle.BackColor = _surface;
        _grid.DefaultCellStyle.ForeColor = _primaryText;
        _grid.DefaultCellStyle.SelectionBackColor = _accentColor;
        _grid.DefaultCellStyle.SelectionForeColor = GetContrastingTextColor(_accentColor);
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = _alternateSurface;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = light
            ? Color.FromArgb(242, 242, 242)
            : Color.FromArgb(45, 45, 45);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = _secondaryText;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor =
            _grid.ColumnHeadersDefaultCellStyle.BackColor;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = _secondaryText;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10F);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
        _grid.ColumnHeadersHeight = 40;
        _statusStrip.BackColor = _surface;
        _statusStrip.ForeColor = _secondaryText;
        Theme.ApplyControlTheme(_grid, light);
    }

    private void FitWindowToRows()
    {
        var nonClientHeight = Height - ClientSize.Height;
        var rowsHeight = _grid.Rows.GetRowsHeight(
            DataGridViewElementStates.Visible);
        var desiredHeight = nonClientHeight +
            _grid.ColumnHeadersHeight +
            rowsHeight +
            _statusStrip.Height +
            2;

        var workingArea = Screen.FromControl(this).WorkingArea;
        var maximumHeight = Math.Max(
            MinimumSize.Height,
            workingArea.Height - 32);
        Height = Math.Clamp(
            desiredHeight,
            MinimumSize.Height,
            maximumHeight);

        if (Bottom > workingArea.Bottom)
            Top = workingArea.Bottom - Height;
        if (Top < workingArea.Top)
            Top = workingArea.Top;
    }

    private static Color GetContrastingTextColor(Color background)
    {
        var brightness =
            ((background.R * 299) + (background.G * 587) + (background.B * 114)) /
            1000;
        return brightness >= 150 ? Color.Black : Color.White;
    }

    private void InitializeContextMenu()
    {
        var refreshItem = new ToolStripMenuItem("Refresh Now");
        refreshItem.Click += async (_, _) => await RefreshBoardAsync();

        var updateItem = new ToolStripMenuItem("Check for Updates…");
        updateItem.Click += async (_, _) => await CheckForUpdatesAsync(updateItem);

        _copyRowItem.Enabled = false;
        _copyRowItem.Click += (_, _) => CopyContextRow();

        _alwaysOnTopItem.CheckedChanged += (_, _) =>
        {
            TopMost = _alwaysOnTopItem.Checked;
        };

        _autoThemeItem.Click += (_, _) => SetThemeMode(ThemeMode.Auto);
        _lightThemeItem.Click += (_, _) => SetThemeMode(ThemeMode.Light);
        _darkThemeItem.Click += (_, _) => SetThemeMode(ThemeMode.Dark);

        var themeItem = new ToolStripMenuItem("Theme");
        themeItem.DropDownItems.Add(_autoThemeItem);
        themeItem.DropDownItems.Add(_lightThemeItem);
        themeItem.DropDownItems.Add(_darkThemeItem);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += (_, _) =>
        {
            _copyRowItem.Enabled =
                _contextRowIndex >= 0 && _contextRowIndex < _grid.Rows.Count;
        };
        contextMenu.Items.Add(_copyRowItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(refreshItem);
        contextMenu.Items.Add(updateItem);
        var connectionItem = new ToolStripMenuItem("Tenant, Client && Group IDs…");
        connectionItem.Click += (_, _) =>
        {
            using var dialog = new ConnectionSettingsForm(_config);
            if (dialog.ShowDialog(this) == DialogResult.OK)
                Application.Restart();
        };
        contextMenu.Items.Add(connectionItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(themeItem);
        contextMenu.Items.Add(_alwaysOnTopItem);
        _grid.ContextMenuStrip = contextMenu;

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ShowUserDetails(e.RowIndex);
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _grid.CurrentRow is not null)
            {
                e.SuppressKeyPress = true;
                ShowUserDetails(_grid.CurrentRow.Index);
            }
        };

        _grid.Sorted += (_, _) =>
        {
            _preferences.SortColumn = _grid.SortedColumn?.Name;
            _preferences.SortDescending =
                _grid.SortOrder == SortOrder.Descending;
        };

        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;

            _contextRowIndex = e.RowIndex;
            _grid.ClearSelection();

            if (_contextRowIndex >= 0)
            {
                _grid.Rows[_contextRowIndex].Selected = true;
                _grid.CurrentCell = _grid.Rows[_contextRowIndex].Cells[0];
            }
        };
    }

    private void ShowUserDetails(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].Tag is not BoardRowData row)
            return;

        try
        {
            var uri = TeamsChat.CreateUri(row.User.UserPrincipalName ?? row.User.Mail, _config.TenantId);
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to open Teams chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyContextRow()
    {
        if (_contextRowIndex < 0 || _contextRowIndex >= _grid.Rows.Count)
            return;

        var values = _grid.Rows[_contextRowIndex]
            .Cells
            .Cast<DataGridViewCell>()
            .Select(cell => cell.FormattedValue?.ToString() ?? "");

        try
        {
            Clipboard.SetDataObject(
                string.Join('\t', values),
                copy: true,
                retryTimes: 5,
                retryDelay: 100);
            _statusLabel.Text = "Row copied to clipboard";
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine(ex);
            _statusLabel.Text = "Clipboard is busy";
            MessageBox.Show(
                this,
                "Windows could not access the clipboard. Please try Copy again.",
                "Clipboard Unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SetThemeMode(ThemeMode mode)
    {
        _themeMode = mode;
        _preferences.Theme = mode.ToString();
        _autoThemeItem.Checked = mode == ThemeMode.Auto;
        _lightThemeItem.Checked = mode == ThemeMode.Light;
        _darkThemeItem.Checked = mode == ThemeMode.Dark;

        var light = mode switch
        {
            ThemeMode.Light => true,
            ThemeMode.Dark => false,
            _ => Theme.IsLightMode()
        };

        ApplyTheme(light);
        Theme.ApplyTitleBarTheme(this, light);
        _grid.Invalidate();
    }

    private void OnWindowsUserPreferenceChanged(
        object sender,
        Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(() => SetThemeMode(_themeMode));
            return;
        }

        SetThemeMode(_themeMode);
    }

    private void RestoreWindowPlacement()
    {
        if (_preferences.WindowX is not int x ||
            _preferences.WindowY is not int y ||
            _preferences.WindowWidth is not int width ||
            _preferences.WindowHeight is not int height ||
            width < MinimumSize.Width ||
            height < MinimumSize.Height)
        {
            return;
        }

        var bounds = new Rectangle(x, y, width, height);
        if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
            return;

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        if (_preferences.Maximized)
            WindowState = FormWindowState.Maximized;
    }

    private void SavePreferences()
    {
        var bounds = WindowState == FormWindowState.Normal
            ? Bounds
            : RestoreBounds;

        _preferences.WindowX = bounds.X;
        _preferences.WindowY = bounds.Y;
        _preferences.WindowWidth = bounds.Width;
        _preferences.WindowHeight = bounds.Height;
        _preferences.Maximized = WindowState == FormWindowState.Maximized;
        _preferences.AlwaysOnTop = TopMost;
        _preferences.Save();
    }

    private async Task CheckForUpdatesAsync(ToolStripMenuItem menuItem)
    {
        menuItem.Enabled = false;
        _statusLabel.Text = "Checking for updates…";

        try
        {
            var result = await _updateChecker.CheckAsync(AppInfo.CurrentVersion, _lifetimeCancellation.Token);
            _lifetimeCancellation.Token.ThrowIfCancellationRequested();
            _statusLabel.Text = result.Message;

            if (!result.UpdateAvailable)
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "TeamsIO Updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var notes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "No release notes were provided."
                : result.ReleaseNotes.Trim();
            var light = _themeMode switch
            {
                ThemeMode.Light => true,
                ThemeMode.Dark => false,
                _ => Theme.IsLightMode()
            };
            using var updateDialog = new UpdateAvailableForm(
                result.AvailableVersion ?? "New version",
                notes,
                light,
                _accentColor);
            var choice = updateDialog.ShowDialog(this);

            if (choice == DialogResult.Yes)
            {
                _statusLabel.Text = "Downloading update from GitHub…";
                var installerPath = await _updateChecker.DownloadInstallerAsync(
                    result,
                    _lifetimeCancellation.Token);
                _lifetimeCancellation.Token.ThrowIfCancellationRequested();
                _statusLabel.Text = "Update downloaded and verified";

                var install = MessageBox.Show(
                    this,
                    $"TeamsIO {result.AvailableVersion} was downloaded and its " +
                    "SHA-256 checksum was verified.\n\n" +
                    "Install it now? TeamsIO will close after starting Setup.",
                    "Install TeamsIO Update",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (install == DialogResult.Yes)
                {
                    await _updateChecker.StartInstallerAsync(installerPath, result, _lifetimeCancellation.Token);
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed || _lifetimeCancellation.IsCancellationRequested) return;
            ShowRefreshError("Update check failed", ex);
            MessageBox.Show(
                this,
                "TeamsIO could not check or download the GitHub update.\n\n" +
                "Verify your network connection, " +
                "the repository releases, then try again.\n\n" +
                ex.Message,
                "Update Check Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed) menuItem.Enabled = true;
        }
    }
}
