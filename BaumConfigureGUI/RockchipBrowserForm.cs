using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

/// <summary>
/// Modal dialog for browsing and downloading Ubuntu Rockchip images
/// from https://github.com/Joshua-Riek/ubuntu-rockchip/releases
/// </summary>
public class RockchipBrowserForm : Form
{
    // ── Controls ──────────────────────────────────────────────────────────────
    private ListView     _releaseList = null!;
    private ListView     _assetList   = null!;
    private RichTextBox  _logBox      = null!;
    private ProgressBar  _progress    = null!;
    private Button       _downloadBtn = null!;
    private Button       _refreshBtn  = null!;
    private Label        _statusLbl   = null!;
    private DarkTextBox  _saveDirBox  = null!;

    private List<RockchipRelease> _releases = [];
    private RockchipAsset?        _selected = null;
    private CancellationTokenSource? _cts   = null;

    /// <summary>Path to the final .img file, set after a successful download.</summary>
    public string? DownloadedImagePath { get; private set; }

    /// <summary>Pre-fill the save folder before the dialog is shown.</summary>
    public string? PresetSaveDir { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    public RockchipBrowserForm()
    {
        BuildUi();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (!string.IsNullOrEmpty(PresetSaveDir))
            _saveDirBox.Text = PresetSaveDir;
        _ = LoadReleasesAsync();
    }

    // ── UI ────────────────────────────────────────────────────────────────────
    private void BuildUi()
    {
        Text          = "Ubuntu Rockchip Image Browser";
        Size          = new Size(980, 660);
        MinimumSize   = new Size(800, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor     = AppTheme.BgMain;
        ForeColor     = AppTheme.TextPrimary;
        Font          = AppTheme.FontBody;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        }
        catch { }

        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 4,
            ColumnCount = 1,
            Padding     = new Padding(12),
            BackColor   = AppTheme.BgMain,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // lists + log
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // save path
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // status bar

        root.Controls.Add(BuildHeader(),    0, 0);
        root.Controls.Add(BuildMain(),      0, 1);
        root.Controls.Add(BuildSaveRow(),   0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);

        Controls.Add(root);
    }

    private Panel BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep, Padding = new Padding(8, 0, 8, 0) };

        var title = new Label
        {
            Text      = "Ubuntu Rockchip",
            Dock      = DockStyle.Left,
            Width     = 200,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
        };

        var sub = new Label
        {
            Text      = "github.com/Joshua-Riek/ubuntu-rockchip",
            Dock      = DockStyle.Left,
            AutoSize  = false,
            Width     = 300,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
        };

        _refreshBtn = ThemedBtn("Refresh", 80, false);
        _refreshBtn.Dock   = DockStyle.Right;
        _refreshBtn.Width  = 80;
        _refreshBtn.Click += (_, _) => _ = LoadReleasesAsync();

        panel.Controls.AddRange([title, sub, _refreshBtn]);
        return panel;
    }

    private Control BuildMain()
    {
        var outer = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 1,
            ColumnCount = 2,
            BackColor   = AppTheme.BgMain,
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

        outer.Controls.Add(BuildListsPanel(), 0, 0);
        outer.Controls.Add(BuildLogPanel(),   1, 0);
        return outer;
    }

    private Control BuildListsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock      = DockStyle.Fill,
            RowCount  = 2,
            ColumnCount = 1,
            BackColor = AppTheme.BgMain,
            Padding   = new Padding(0, 4, 6, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        // ── Releases list ─────────────────────────────────────────────────────
        var relPanel = new Panel { Dock = DockStyle.Fill };
        relPanel.Controls.Add(SectionLabel("Releases", 0, 0));

        _releaseList = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            MultiSelect   = false,
            BackColor     = AppTheme.BgPanel,
            ForeColor     = AppTheme.TextPrimary,
            BorderStyle   = BorderStyle.None,
            Font          = AppTheme.FontSmall,
            GridLines     = false,
        };
        _releaseList.Columns.Add("Tag",      110);
        _releaseList.Columns.Add("Released", 90);
        _releaseList.Columns.Add("Images",   50);
        _releaseList.SelectedIndexChanged += OnReleaseSelected;
        var relHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
        relHeader.Controls.Add(SectionLabel("Releases", 0, 2));
        relPanel.Controls.Add(_releaseList);
        relPanel.Controls.Add(relHeader);

        // ── Assets list ───────────────────────────────────────────────────────
        var assetPanel = new Panel { Dock = DockStyle.Fill };
        _assetList = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            MultiSelect   = false,
            BackColor     = AppTheme.BgPanel,
            ForeColor     = AppTheme.TextPrimary,
            BorderStyle   = BorderStyle.None,
            Font          = AppTheme.FontSmall,
        };
        _assetList.Columns.Add("Image File", 230);
        _assetList.Columns.Add("Size",       70);
        _assetList.SelectedIndexChanged += OnAssetSelected;
        var assetHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
        assetHeader.Controls.Add(SectionLabel("Image Files", 0, 2));
        assetPanel.Controls.Add(_assetList);
        assetPanel.Controls.Add(assetHeader);

        panel.Controls.Add(relPanel,   0, 0);
        panel.Controls.Add(assetPanel, 0, 1);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 3,
            ColumnCount = 1,
            BackColor   = AppTheme.BgMain,
            Padding     = new Padding(6, 4, 0, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // label
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // log
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // progress

        panel.Controls.Add(SectionLabel("Download Output", 0, 0), 0, 0);

        _logBox = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BackColor   = AppTheme.BgDeep,
            ForeColor   = Color.FromArgb(180, 220, 130),
            Font        = AppTheme.FontMono,
            BorderStyle = BorderStyle.None,
            WordWrap    = true,
        };
        panel.Controls.Add(_logBox, 0, 1);

        _progress = new ProgressBar
        {
            Dock    = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Style   = ProgressBarStyle.Continuous,
            Visible = false,
        };
        panel.Controls.Add(_progress, 0, 2);
        return panel;
    }

    private Panel BuildSaveRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgMain, Padding = new Padding(0, 4, 0, 0) };

        panel.Controls.Add(new Label
        {
            Text      = "Save To:",
            Location  = new Point(0, 8),
            Width     = 70,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = AppTheme.TextSecondary,
            Font      = AppTheme.FontSmall,
            BackColor = Color.Transparent,
        });

        _saveDirBox = new DarkTextBox { Location = new Point(76, 4), Width = 560, PlaceholderText = "Select download folder…" };
        panel.Controls.Add(_saveDirBox);

        var browseBtn = ThemedBtn("Browse…", 70, false);
        browseBtn.Location = new Point(644, 4);
        browseBtn.Click   += BrowseSaveDir;
        panel.Controls.Add(browseBtn);

        _downloadBtn = ThemedBtn("Download Image", 140, true);
        _downloadBtn.Location  = new Point(720, 3);
        _downloadBtn.Width     = 160;
        _downloadBtn.Height    = 30;
        _downloadBtn.Enabled   = false;
        _downloadBtn.Click    += OnDownloadClicked;
        panel.Controls.Add(_downloadBtn);

        return panel;
    }

    private Panel BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep, Padding = new Padding(4, 0, 4, 0) };
        _statusLbl = new Label
        {
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
            Text      = "Loading releases…",
        };
        panel.Controls.Add(_statusLbl);
        return panel;
    }

    // ── Data loading ──────────────────────────────────────────────────────────
    private async Task LoadReleasesAsync()
    {
        _releaseList.Items.Clear();
        _assetList.Items.Clear();
        _refreshBtn.Enabled = false;
        SetStatus("Fetching releases from GitHub…");

        try
        {
            _releases = await RockchipImageService.FetchReleasesAsync(15);

            _releaseList.Items.Clear();
            foreach (var r in _releases)
            {
                var item = new ListViewItem(r.TagName);
                item.SubItems.Add(r.PublishedAt);
                item.SubItems.Add(r.Assets.Count.ToString());
                item.Tag = r;
                _releaseList.Items.Add(item);
            }

            SetStatus($"Loaded {_releases.Count} releases. Select a release to see available images.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load releases: {ex.Message}");
            Log($"Error: {ex.Message}");
        }
        finally
        {
            _refreshBtn.Enabled = true;
        }
    }

    private void OnReleaseSelected(object? s, EventArgs e)
    {
        _assetList.Items.Clear();
        _downloadBtn.Enabled = false;
        _selected = null;

        if (_releaseList.SelectedItems.Count == 0) return;
        var release = (RockchipRelease)_releaseList.SelectedItems[0].Tag!;

        foreach (var asset in release.Assets)
        {
            var size = asset.SizeBytes >= 1_048_576
                ? $"{asset.SizeBytes / 1_048_576.0:F0} MB"
                : $"{asset.SizeBytes / 1024} KB";
            var item = new ListViewItem(asset.Name);
            item.SubItems.Add(size);
            item.Tag = asset;
            _assetList.Items.Add(item);
        }

        SetStatus($"{release.TagName} — {release.Assets.Count} image(s) available. Select one to download.");
    }

    private void OnAssetSelected(object? s, EventArgs e)
    {
        _selected            = null;
        _downloadBtn.Enabled = false;

        if (_assetList.SelectedItems.Count == 0) return;
        _selected = (RockchipAsset)_assetList.SelectedItems[0].Tag!;

        _downloadBtn.Enabled = !string.IsNullOrEmpty(_saveDirBox.Text)
                            && Directory.Exists(_saveDirBox.Text);
        SetStatus($"Selected: {_selected.Name}");
    }

    // ── Download ──────────────────────────────────────────────────────────────
    private async void OnDownloadClicked(object? s, EventArgs e)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            return;
        }

        if (_selected is null) return;

        var saveDir = _saveDirBox.Text.Trim();
        if (!Directory.Exists(saveDir))
        {
            MessageBox.Show("Please select a valid save folder.", "Save Folder Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _cts                 = new CancellationTokenSource();
        _downloadBtn.Text    = "Cancel";
        _downloadBtn.BackColor = AppTheme.Danger;
        _progress.Value      = 0;
        _progress.Visible    = true;
        _logBox.Clear();
        SetStatus("Downloading…");

        try
        {
            var imgPath = await RockchipImageService.DownloadAsync(
                _selected,
                saveDir,
                pct =>
                {
                    if (_progress.InvokeRequired) _progress.Invoke(() => _progress.Value = pct);
                    else _progress.Value = pct;
                },
                Log,
                _cts.Token);

            DownloadedImagePath = imgPath;
            SetStatus($"Ready: {Path.GetFileName(imgPath)}");
            Log($"\nImage saved to: {imgPath}");
            Log("Click Use This Image to set it as the base image in the builder.");

            _downloadBtn.Text      = "Use This Image";
            _downloadBtn.BackColor = AppTheme.Success;
            _downloadBtn.Click    -= OnDownloadClicked;
            _downloadBtn.Click    += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        }
        catch (OperationCanceledException)
        {
            Log("Download cancelled.");
            SetStatus("Cancelled.");
            ResetDownloadButton();
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            SetStatus($"Download failed: {ex.Message}");
            ResetDownloadButton();
        }
        finally
        {
            _cts?.Dispose();
            _cts              = null;
            _progress.Visible = false;
        }
    }

    private void ResetDownloadButton()
    {
        _downloadBtn.Text      = "Download Image";
        _downloadBtn.BackColor = AppTheme.Accent;
    }

    // ── Browse ────────────────────────────────────────────────────────────────
    private void BrowseSaveDir(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description        = "Select folder to save downloaded image",
            UseDescriptionForTitle = true,
            InitialDirectory   = _saveDirBox.Text,
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _saveDirBox.Text     = dlg.SelectedPath;
            _downloadBtn.Enabled = _selected != null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void Log(string msg)
    {
        if (_logBox.InvokeRequired) { _logBox.Invoke(() => Log(msg)); return; }
        _logBox.AppendText(msg + "\n");
        _logBox.ScrollToCaret();
    }

    private void SetStatus(string msg)
    {
        if (_statusLbl.InvokeRequired) { _statusLbl.Invoke(() => SetStatus(msg)); return; }
        _statusLbl.Text = msg;
    }

    private static Label SectionLabel(string text, int x, int y) =>
        new()
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            Font      = AppTheme.FontHeader,
            ForeColor = AppTheme.Accent,
            BackColor = Color.Transparent,
        };

    private static Button ThemedBtn(string text, int width, bool accent) =>
        new()
        {
            Text      = text,
            Width     = width,
            Height    = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? AppTheme.Accent : AppTheme.BgCard,
            ForeColor = accent ? Color.White : AppTheme.TextSecondary,
            Font      = AppTheme.FontSmall,
            Cursor    = Cursors.Hand,
        };
}
