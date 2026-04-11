using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

/// <summary>
/// Modal dialog for browsing and downloading Raspberry Pi OS images
/// from https://downloads.raspberrypi.com
/// </summary>
public class RaspberryPiBrowserForm : Form
{
    private ListView     _categoryList = null!;
    private ListView     _imageList    = null!;
    private RichTextBox  _logBox       = null!;
    private ProgressBar  _progress     = null!;
    private Button       _downloadBtn  = null!;
    private Button       _refreshBtn   = null!;
    private Label        _statusLbl    = null!;
    private DarkTextBox  _saveDirBox   = null!;

    private List<PiCategory> _categories = [];
    private PiImage?         _selected   = null;
    private CancellationTokenSource? _cts = null;

    public string? DownloadedImagePath { get; private set; }
    public string? PresetSaveDir       { get; set; }

    public RaspberryPiBrowserForm() => BuildUi();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (!string.IsNullOrEmpty(PresetSaveDir))
            _saveDirBox.Text = PresetSaveDir;
        _ = LoadCategoriesAsync();
    }

    // ── UI ────────────────────────────────────────────────────────────────────
    private void BuildUi()
    {
        Text          = "Raspberry Pi Image Browser";
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
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
            Padding = new Padding(12), BackColor = AppTheme.BgMain,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.Controls.Add(BuildHeader(),    0, 0);
        root.Controls.Add(BuildMain(),      0, 1);
        root.Controls.Add(BuildSaveRow(),   0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);

        Controls.Add(root);
    }

    private Panel BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep, Padding = new Padding(8, 0, 8, 0) };

        panel.Controls.Add(new Label
        {
            Text = "Raspberry Pi OS", Dock = DockStyle.Left, Width = 200,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent,
        });
        panel.Controls.Add(new Label
        {
            Text = "downloads.raspberrypi.com", Dock = DockStyle.Left, Width = 240,
            TextAlign = ContentAlignment.MiddleLeft, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted, BackColor = Color.Transparent,
        });

        _refreshBtn       = ThemedBtn("Refresh", 80, false);
        _refreshBtn.Dock  = DockStyle.Right;
        _refreshBtn.Width = 80;
        _refreshBtn.Click += (_, _) => _ = LoadCategoriesAsync();
        panel.Controls.Add(_refreshBtn);
        return panel;
    }

    private Control BuildMain()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, BackColor = AppTheme.BgMain,
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
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            BackColor = AppTheme.BgMain, Padding = new Padding(0, 4, 6, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

        // Category list
        var catPanel = new Panel { Dock = DockStyle.Fill };
        _categoryList = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            MultiSelect = false, BackColor = AppTheme.BgPanel, ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.None, Font = AppTheme.FontSmall,
        };
        _categoryList.Columns.Add("Category", 230);
        _categoryList.Columns.Add("Images",    50);
        _categoryList.SelectedIndexChanged += OnCategorySelected;
        var catHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
        catHeader.Controls.Add(SectionLabel("Categories", 0, 2));
        catPanel.Controls.Add(_categoryList);
        catPanel.Controls.Add(catHeader);

        // Image list
        var imgPanel = new Panel { Dock = DockStyle.Fill };
        _imageList = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            MultiSelect = false, BackColor = AppTheme.BgPanel, ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.None, Font = AppTheme.FontSmall,
        };
        _imageList.Columns.Add("Image",    160);
        _imageList.Columns.Add("Released",  80);
        _imageList.Columns.Add("Size",      60);
        _imageList.SelectedIndexChanged += OnImageSelected;
        var imgHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
        imgHeader.Controls.Add(SectionLabel("Images", 0, 2));
        imgPanel.Controls.Add(_imageList);
        imgPanel.Controls.Add(imgHeader);

        panel.Controls.Add(catPanel, 0, 0);
        panel.Controls.Add(imgPanel, 0, 1);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            BackColor = AppTheme.BgMain, Padding = new Padding(6, 4, 0, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        panel.Controls.Add(SectionLabel("Download Output", 0, 0), 0, 0);

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BackColor = AppTheme.BgDeep,
            ForeColor = Color.FromArgb(180, 220, 130), Font = AppTheme.FontMono,
            BorderStyle = BorderStyle.None, WordWrap = true,
        };
        panel.Controls.Add(_logBox, 0, 1);

        _progress = new ProgressBar
        {
            Dock = DockStyle.Fill, Minimum = 0, Maximum = 100,
            Style = ProgressBarStyle.Continuous, Visible = false,
        };
        panel.Controls.Add(_progress, 0, 2);
        return panel;
    }

    private Panel BuildSaveRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgMain, Padding = new Padding(0, 4, 0, 0) };

        panel.Controls.Add(new Label
        {
            Text = "Save To:", Location = new Point(0, 8), Width = 70,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = AppTheme.TextSecondary, Font = AppTheme.FontSmall, BackColor = Color.Transparent,
        });

        _saveDirBox = new DarkTextBox { Location = new Point(76, 4), Width = 560, PlaceholderText = "Select download folder…" };
        panel.Controls.Add(_saveDirBox);

        var browseBtn = ThemedBtn("Browse…", 70, false);
        browseBtn.Location = new Point(644, 4);
        browseBtn.Click   += BrowseSaveDir;
        panel.Controls.Add(browseBtn);

        _downloadBtn           = ThemedBtn("Download Image", 140, true);
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
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent, Text = "Loading image list…",
        };
        panel.Controls.Add(_statusLbl);
        return panel;
    }

    // ── Data ──────────────────────────────────────────────────────────────────
    private async Task LoadCategoriesAsync()
    {
        _categoryList.Items.Clear();
        _imageList.Items.Clear();
        _refreshBtn.Enabled = false;
        SetStatus("Fetching image lists…");

        try
        {
            // Fetch Pi Imager JSON and Ubuntu Simplestreams in parallel;
            // each is wrapped so a single source failure doesn't kill the other.
            var piTask     = SafeFetchAsync(() => RaspberryPiImageService.FetchCategoriesAsync(SetStatus));
            var ubuntuTask = SafeFetchAsync(() => UbuntuRpiImageService.FetchCategoriesAsync(SetStatus));

            await Task.WhenAll(piTask, ubuntuTask);

            _categories = [.. piTask.Result.Concat(ubuntuTask.Result)
                                           .OrderBy(c => c.Name)];

            foreach (var cat in _categories)
            {
                var item = new ListViewItem(cat.Name);
                item.SubItems.Add(cat.Images.Count.ToString());
                item.Tag = cat;
                _categoryList.Items.Add(item);
            }
            SetStatus($"Loaded {_categories.Count} categories. Select one to see available images.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed: {ex.Message}");
            Log($"Error: {ex.Message}");
        }
        finally { _refreshBtn.Enabled = true; }
    }

    /// Runs <paramref name="fetch"/> and returns its result, or an empty list on any error.
    private static async Task<List<PiCategory>> SafeFetchAsync(Func<Task<List<PiCategory>>> fetch)
    {
        try   { return await fetch(); }
        catch { return [];            }
    }

    private void OnCategorySelected(object? s, EventArgs e)
    {
        _imageList.Items.Clear();
        _downloadBtn.Enabled = false;
        _selected = null;

        if (_categoryList.SelectedItems.Count == 0) return;
        var cat = (PiCategory)_categoryList.SelectedItems[0].Tag!;

        foreach (var img in cat.Images)
        {
            var size = img.DownloadSize >= 1_048_576
                ? $"{img.DownloadSize / 1_048_576.0:F0} MB"
                : img.DownloadSize > 0 ? $"{img.DownloadSize / 1024} KB" : "—";
            var item = new ListViewItem(img.Name);
            item.SubItems.Add(img.ReleaseDate);
            item.SubItems.Add(size);
            item.Tag = img;
            _imageList.Items.Add(item);
        }
        SetStatus($"{cat.Name} — {cat.Images.Count} image(s). Select one to download.");
    }

    private void OnImageSelected(object? s, EventArgs e)
    {
        _selected = null;
        _downloadBtn.Enabled = false;
        if (_imageList.SelectedItems.Count == 0) return;
        _selected = (PiImage)_imageList.SelectedItems[0].Tag!;
        _downloadBtn.Enabled = !string.IsNullOrEmpty(_saveDirBox.Text) && Directory.Exists(_saveDirBox.Text);
        SetStatus($"Selected: {_selected.Name}");
    }

    // ── Download ──────────────────────────────────────────────────────────────
    private async void OnDownloadClicked(object? s, EventArgs e)
    {
        if (_cts != null) { _cts.Cancel(); return; }
        if (_selected is null) return;

        var saveDir = _saveDirBox.Text.Trim();
        if (!Directory.Exists(saveDir))
        {
            MessageBox.Show("Please select a valid save folder.", "Save Folder Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _cts                      = new CancellationTokenSource();
        _downloadBtn.Text         = "Cancel";
        _downloadBtn.BackColor    = AppTheme.Danger;
        _progress.Value           = 0;
        _progress.Visible         = true;
        _logBox.Clear();
        SetStatus("Downloading…");

        try
        {
            var imgPath = await RaspberryPiImageService.DownloadAsync(
                _selected, saveDir,
                pct =>
                {
                    if (_progress.InvokeRequired) _progress.Invoke(() => _progress.Value = pct);
                    else _progress.Value = pct;
                },
                Log, _cts.Token);

            DownloadedImagePath = imgPath;
            SetStatus($"Ready: {Path.GetFileName(imgPath)}");
            Log($"\nImage saved to: {imgPath}");
            Log("Click Use This Image to set it as the base image in the builder.");

            _downloadBtn.Text       = "Use This Image";
            _downloadBtn.BackColor  = AppTheme.Success;
            _downloadBtn.Click     -= OnDownloadClicked;
            _downloadBtn.Click     += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        }
        catch (OperationCanceledException)
        {
            Log("Download cancelled.");
            SetStatus("Cancelled.");
            ResetBtn();
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            SetStatus($"Failed: {ex.Message}");
            ResetBtn();
        }
        finally
        {
            _cts?.Dispose();
            _cts              = null;
            _progress.Visible = false;
        }
    }

    private void ResetBtn()
    {
        _downloadBtn.Text      = "Download Image";
        _downloadBtn.BackColor = AppTheme.Accent;
    }

    private void BrowseSaveDir(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select folder to save downloaded image",
            UseDescriptionForTitle = true, InitialDirectory = _saveDirBox.Text,
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
        new() { Text = text, Location = new Point(x, y), AutoSize = true,
            Font = AppTheme.FontHeader, ForeColor = AppTheme.Accent, BackColor = Color.Transparent };

    private static Button ThemedBtn(string text, int width, bool accent) =>
        new() { Text = text, Width = width, Height = 28, FlatStyle = FlatStyle.Flat,
            BackColor = accent ? AppTheme.Accent : AppTheme.BgCard,
            ForeColor = accent ? Color.White : AppTheme.TextSecondary,
            Font = AppTheme.FontSmall, Cursor = Cursors.Hand };
}
