using System.Text.Json;
using BaumConfigureGUI.Models;
using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

public class MainForm : Form
{
    // ── Config controls ───────────────────────────────────────────────────────
    private DarkTextBox   _hostnameBox     = null!;
    private DarkTextBox   _usernameBox     = null!;
    private DarkTextBox   _passwordBox     = null!;
    private DarkTextBox   _passwordConfBox = null!;
    private Label         _passwordStrLbl  = null!;
    private DarkTextBox   _timezoneBox     = null!;
    private DarkCheckBox  _dockerCheck     = null!;
    private DarkCheckBox  _k8sCheck        = null!;
    private DarkCheckBox  _portainerCheck  = null!;
    private DarkTextBox   _extraPkgBox     = null!;
    private DarkTextBox   _extraCmdBox     = null!;
    private DarkCheckBox  _weeklyUpdateCheck = null!;

    // ── Path controls ─────────────────────────────────────────────────────────
    private DarkTextBox _baseImageBox = null!;
    private DarkTextBox _outputDirBox = null!;
    private DarkTextBox _wslDistroBox = null!;

    // ── Output / status ───────────────────────────────────────────────────────
    private RichTextBox _outputBox       = null!;
    private Button      _buildBtn        = null!;
    private Label       _statusLabel     = null!;
    private ProgressBar _progressBar     = null!;
    private Label       _updateBadge     = null!;
    private ReleaseInfo? _pendingUpdate  = null;

    private CancellationTokenSource? _buildCts;
    private DateTime _lastUpdateCheck = DateTime.MinValue;

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BaumConfigure", "settings.json");

    // ═════════════════════════════════════════════════════════════════════════
    public MainForm()
    {
        BuildUi();
        LoadSettings();
        _ = CheckForUpdateAsync();
    }

    // ── UI construction ───────────────────────────────────────────────────────
    private void BuildUi()
    {
        Text          = "BaumConfigure";
        Size          = new Size(1080, 920);
        MinimumSize   = new Size(880, 840);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = AppTheme.BgMain;
        ForeColor     = AppTheme.TextPrimary;
        Font          = AppTheme.FontBody;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        }
        catch { /* icon optional */ }

        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 3,
            ColumnCount = 1,
            Padding     = new Padding(0),
            BackColor   = AppTheme.BgMain,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));   // title bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // body
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // status bar

        root.Controls.Add(BuildTitleBar(),  0, 0);
        root.Controls.Add(BuildBody(),      0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);

        Controls.Add(root);
    }

    private Panel BuildTitleBar()
    {
        var panel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = AppTheme.BgDeep,
            Padding   = new Padding(16, 0, 16, 0),
        };

        var title = new Label
        {
            Text      = "BaumConfigure",
            Dock      = DockStyle.Left,
            AutoSize  = false,
            Width     = 240,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
        };

        var sub = new Label
        {
            Text      = "OS Image Builder",
            Dock      = DockStyle.Left,
            AutoSize  = false,
            Width     = 160,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
        };

        var ver = new Label
        {
            Text      = $"v{UpdateService.CurrentVersion}",
            Dock      = DockStyle.Right,
            AutoSize  = false,
            Width     = 60,
            TextAlign = ContentAlignment.MiddleRight,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
        };

        _updateBadge = new Label
        {
            Text      = "Update available",
            Dock      = DockStyle.Right,
            AutoSize  = false,
            Width     = 110,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.BgMain,
            BackColor = AppTheme.Warning,
            Cursor    = Cursors.Hand,
            Visible   = false,
        };
        _updateBadge.Click += OnUpdateBadgeClicked;

        panel.Controls.AddRange([title, sub, ver, _updateBadge]);
        return panel;
    }

    private Control BuildBody()
    {
        var split = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Vertical,
            SplitterWidth = 4,
            BackColor     = AppTheme.Border,
        };
        // Panel1/2MinSize and SplitterDistance all call set_SplitterDistance
        // internally, which requires the control to have a valid width.
        // Defer until the form is fully loaded and sized.
        Load += (_, _) =>
        {
            split.Panel1MinSize = 380;
            split.Panel2MinSize = 320;
            try { split.SplitterDistance = 440; } catch { }
        };
        split.Panel1.BackColor = AppTheme.BgMain;
        split.Panel2.BackColor = AppTheme.BgMain;

        split.Panel1.Controls.Add(BuildConfigPanel());
        split.Panel2.Controls.Add(BuildOutputPanel());
        return split;
    }

    // ── Config panel ──────────────────────────────────────────────────────────
    private Control BuildConfigPanel()
    {
        var scroll = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = AppTheme.BgMain,
            Padding    = new Padding(18, 14, 14, 14),
        };

        const int LW = 128;
        const int FX = 136;
        const int FW = 264;
        int y = 6;

        void Section(string title)
        {
            y += 4;
            var lbl = new Label
            {
                Text      = title,
                Location  = new Point(0, y),
                AutoSize  = true,
                Font      = AppTheme.FontHeader,
                ForeColor = AppTheme.Accent,
                BackColor = Color.Transparent,
            };
            scroll.Controls.Add(lbl);
            y += 20;
            scroll.Controls.Add(new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(FX + FW + 40, 1),
                BackColor = AppTheme.Border,
            });
            y += 8;
        }

        DarkTextBox Field(string label, string val, bool password = false, string? placeholder = null)
        {
            scroll.Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(0, y + 4),
                Width     = LW,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = AppTheme.FontSmall,
                ForeColor = AppTheme.TextSecondary,
                BackColor = Color.Transparent,
            });
            var tb = new DarkTextBox
            {
                Location  = new Point(FX, y),
                Width     = FW,
                Text      = val,
            };
            if (password) tb.UseSystemPasswordChar = true;
            if (placeholder != null) tb.PlaceholderText = placeholder;
            scroll.Controls.Add(tb);
            y += 30;
            return tb;
        }

        DarkCheckBox Check(string label, bool val, int xOff = 0)
        {
            var cb = new DarkCheckBox
            {
                Text     = label,
                Location = new Point(FX + xOff, y),
                AutoSize = true,
                Checked  = val,
            };
            scroll.Controls.Add(cb);
            return cb;
        }

        // ── Image ─────────────────────────────────────────────────────────────
        Section("Image");

        scroll.Controls.Add(FieldLabel("Base Image:", 0, y, LW));
        _baseImageBox = new DarkTextBox { Location = new Point(FX, y), Width = FW - 66, PlaceholderText = "Select base .img file…" };
        var browseImg = AccentBtn("Browse…", FX + FW - 60, y, 60, 26);
        browseImg.Click += BrowseBaseImage;
        scroll.Controls.AddRange([_baseImageBox, browseImg]);
        y += 30;

        var rockchipBtn = AccentBtn("Get Rockchip Image", FX, y, FW, 24);
        rockchipBtn.FlatAppearance.BorderColor = AppTheme.Accent;
        rockchipBtn.FlatAppearance.BorderSize  = 1;
        rockchipBtn.Click += OpenRockchipBrowser;
        scroll.Controls.Add(rockchipBtn);
        y += 30;

        scroll.Controls.Add(FieldLabel("Output Folder:", 0, y, LW));
        _outputDirBox = new DarkTextBox { Location = new Point(FX, y), Width = FW - 66, PlaceholderText = "Select output folder…" };
        var browseOut = AccentBtn("Browse…", FX + FW - 60, y, 60, 26);
        browseOut.Click += BrowseOutputDir;
        scroll.Controls.AddRange([_outputDirBox, browseOut]);
        y += 30;

        scroll.Controls.Add(FieldLabel("WSL Distro:", 0, y, LW));
        _wslDistroBox = new DarkTextBox { Location = new Point(FX, y), Width = 110, Text = "Ubuntu" };
        scroll.Controls.Add(_wslDistroBox);
        y += 34;

        var checkUpdateBtn = AccentBtn("Check for Updates", FX, y, 150, 24);
        checkUpdateBtn.Click += OnCheckUpdateClicked;
        _weeklyUpdateCheck = new DarkCheckBox
        {
            Text     = "Weekly only",
            Location = new Point(FX + 158, y + 2),
            AutoSize = true,
        };
        scroll.Controls.AddRange([checkUpdateBtn, _weeklyUpdateCheck]);
        y += 32;

        // ── System ────────────────────────────────────────────────────────────
        Section("System");

        _hostnameBox = Field("Hostname:", "ubuntu-server");
        _timezoneBox = Field("Timezone:", "America/New_York", placeholder: "e.g. America/Chicago");
        y += 2;

        // ── User Account ──────────────────────────────────────────────────────
        Section("User Account");

        _usernameBox     = Field("Username:", "ubuntu");
        _passwordBox     = Field("Password:", "", password: true);
        _passwordBox.TextChanged     += OnPasswordChanged;
        _passwordConfBox = Field("Confirm Password:", "", password: true);
        _passwordConfBox.TextChanged += OnPasswordChanged;

        _passwordStrLbl = new Label
        {
            Location  = new Point(FX, y),
            AutoSize  = true,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
            Text      = "Password hashed automatically at build time",
        };
        scroll.Controls.Add(_passwordStrLbl);
        y += 22;

        // ── Software ──────────────────────────────────────────────────────────
        y += 4;
        Section("Software");

        var presetDockerBtn = AccentBtn("Docker Host", FX, y, 90, 22);
        var presetK8sBtn    = AccentBtn("K8s Node",    FX + 96,  y, 80, 22);
        var presetFullBtn   = AccentBtn("Full Stack",  FX + 182, y, 80, 22);
        presetDockerBtn.Click += (_, _) => { _dockerCheck.Checked = true; _k8sCheck.Checked = false; _portainerCheck.Checked = true; };
        presetK8sBtn.Click    += (_, _) => { _dockerCheck.Checked = true; _k8sCheck.Checked = true;  _portainerCheck.Checked = false; };
        presetFullBtn.Click   += (_, _) => { _dockerCheck.Checked = true; _k8sCheck.Checked = true;  _portainerCheck.Checked = true; };
        scroll.Controls.AddRange([presetDockerBtn, presetK8sBtn, presetFullBtn]);
        y += 28;

        _dockerCheck = Check("Docker", false, 0);
        _k8sCheck    = Check("Kubernetes", false, 80);
        y += 28;

        _portainerCheck = Check("Portainer CE  (requires Docker)", false, 0);
        _portainerCheck.Enabled = false;
        y += 28;

        _dockerCheck.CheckedChanged += (_, _) =>
        {
            _portainerCheck.Enabled = _dockerCheck.Checked;
            if (!_dockerCheck.Checked) _portainerCheck.Checked = false;
        };

        _extraPkgBox = Field("Extra Packages:", "", placeholder: "space or comma separated");
        y += 2;

        // ── Extra Commands ────────────────────────────────────────────────────
        Section("First-Boot Commands");

        scroll.Controls.Add(FieldLabel("Commands:", 0, y, LW));
        _extraCmdBox = new DarkTextBox
        {
            Location    = new Point(FX, y),
            Width       = FW,
            Height      = 68,
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            PlaceholderText = "One command per line",
        };
        scroll.Controls.Add(_extraCmdBox);
        y += 76;

        // ── Build button ──────────────────────────────────────────────────────
        y += 12;
        _buildBtn = new Button
        {
            Text      = "Build Image",
            Location  = new Point(FX, y),
            Width     = FW,
            Height    = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.Accent,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        _buildBtn.FlatAppearance.BorderSize  = 0;
        _buildBtn.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
        _buildBtn.Click += OnBuildClicked;
        scroll.Controls.Add(_buildBtn);
        y += 52;

        scroll.AutoScrollMinSize = new Size(0, y);
        return scroll;
    }

    // ── Output panel ──────────────────────────────────────────────────────────
    private Control BuildOutputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 3,
            ColumnCount = 1,
            BackColor   = AppTheme.BgMain,
            Padding     = new Padding(6, 8, 10, 10),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // header
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // console
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // progress

        // Header
        var header = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgMain };
        var outLbl = new Label
        {
            Text      = "Build Output",
            Dock      = DockStyle.Left,
            Width     = 120,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = AppTheme.FontHeader,
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
        };
        var clearBtn = new Button
        {
            Text      = "Clear",
            Dock      = DockStyle.Right,
            Width     = 56,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.TextSecondary,
            Font      = AppTheme.FontSmall,
        };
        clearBtn.FlatAppearance.BorderColor = AppTheme.Border;
        clearBtn.Click += (_, _) => _outputBox.Clear();
        header.Controls.AddRange([outLbl, clearBtn]);

        // Console
        _outputBox = new RichTextBox
        {
            Dock       = DockStyle.Fill,
            ReadOnly   = true,
            BackColor  = AppTheme.BgDeep,
            ForeColor  = Color.FromArgb(180, 220, 130),
            Font       = AppTheme.FontMono,
            WordWrap   = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            BorderStyle = BorderStyle.None,
        };

        // Progress bar
        _progressBar = new ProgressBar
        {
            Dock    = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value   = 0,
            Style   = ProgressBarStyle.Continuous,
            Visible = false,
        };

        panel.Controls.Add(header,       0, 0);
        panel.Controls.Add(_outputBox,   0, 1);
        panel.Controls.Add(_progressBar, 0, 2);
        return panel;
    }

    private Panel BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep, Padding = new Padding(10, 0, 10, 0) };
        _statusLabel = new Label
        {
            Dock      = DockStyle.Fill,
            ForeColor = AppTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = AppTheme.FontSmall,
            Text      = "Ready — select a base image, configure your settings, then click Build Image.",
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    // ── Update logic ──────────────────────────────────────────────────────────
    private async Task CheckForUpdateAsync(bool force = false)
    {
        var weekly   = _weeklyUpdateCheck?.Checked ?? false;
        var lastCheck = _lastUpdateCheck;
        if (!force && !UpdateService.ShouldCheck(weekly, lastCheck)) return;

        try
        {
            var info = await UpdateService.CheckAsync();
            _lastUpdateCheck = DateTime.UtcNow;
            SaveSettings();
            if (info is null) return;
            _pendingUpdate = info;
            if (_updateBadge.InvokeRequired)
                _updateBadge.Invoke(() => _updateBadge.Visible = true);
            else
                _updateBadge.Visible = true;
        }
        catch { /* silent */ }
    }

    private async void OnCheckUpdateClicked(object? s, EventArgs e)
    {
        SetStatus("Checking for updates…");
        await CheckForUpdateAsync(force: true);
        if (_pendingUpdate is null)
            SetStatus("You are up to date.");
    }

    private void OnUpdateBadgeClicked(object? s, EventArgs e)
    {
        if (_pendingUpdate is null) return;

        var result = MessageBox.Show(
            $"BaumConfigure v{_pendingUpdate.Version} is available.\n\n" +
            $"The update will be downloaded and installed automatically.\n" +
            $"The app will restart when complete.\n\n" +
            $"Install now?",
            "Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes) return;

        _ = InstallUpdateAsync(_pendingUpdate);
    }

    private async Task InstallUpdateAsync(ReleaseInfo info)
    {
        _buildBtn.Enabled    = false;
        _updateBadge.Visible = false;
        _progressBar.Visible = true;
        SetStatus("Downloading update…");
        SwitchToOutput();
        Log($"\n── Updating to BaumConfigure v{info.Version} ──");

        try
        {
            await UpdateService.DownloadAndInstallAsync(
                info,
                pct =>
                {
                    if (_progressBar.InvokeRequired) _progressBar.Invoke(() => _progressBar.Value = pct);
                    else _progressBar.Value = pct;
                    if (pct % 10 == 0) Log($"  Download: {pct}%");
                },
                Log);
        }
        catch (Exception ex)
        {
            Log($"✘ Update failed: {ex.Message}");
            SetStatus("Update failed — see output.");
            _buildBtn.Enabled    = true;
            _progressBar.Visible = false;
        }
    }

    // ── Build logic ───────────────────────────────────────────────────────────
    private async void OnBuildClicked(object? s, EventArgs e)
    {
        if (_buildCts != null)
        {
            _buildCts.Cancel();
            return;
        }

        if (!ValidateInputs(out var config, out var baseImage, out var outputPath)) return;

        _buildBtn.Text      = "Cancel";
        _buildBtn.BackColor = AppTheme.Danger;
        _progressBar.Value  = 0;
        _progressBar.Visible = true;
        SetStatus("Building image…");
        _outputBox.Clear();
        _buildCts = new CancellationTokenSource();

        try
        {
            var builder = new ImageBuilderService(_wslDistroBox.Text.Trim());

            if (!string.IsNullOrWhiteSpace(_passwordBox.Text))
            {
                Log("── Hashing password via WSL…");
                SetStatus("Hashing password…");
                try
                {
                    config.PasswordHash = await builder.HashPasswordAsync(_passwordBox.Text);
                    Log("  Password hashed successfully.");
                }
                catch (Exception ex)
                {
                    Log($"✘ Password hashing failed: {ex.Message}");
                    SetStatus("Build failed.");
                    return;
                }
            }

            Log($"── Building image");
            Log($"   Base    : {baseImage}");
            Log($"   Output  : {outputPath}");
            Log($"   Hostname: {config.Hostname}  User: {config.Username}  TZ: {config.Timezone}");
            Log($"   Docker  : {config.InstallDocker}  K8s: {config.InstallK8s}  Portainer: {config.InstallPortainer}");
            Log("");

            await builder.BuildImageAsync(config, baseImage, outputPath, Log, _buildCts.Token);

            if (!_buildCts.IsCancellationRequested)
            {
                _progressBar.Value = 100;
                Log("");
                Log($"✔ Image ready: {outputPath}");
                SetStatus($"Image ready: {Path.GetFileName(outputPath)}");
            }
        }
        catch (OperationCanceledException)
        {
            Log("── Build cancelled.");
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            Log($"✘ Error: {ex.Message}");
            SetStatus("Build failed — see output.");
        }
        finally
        {
            _buildCts?.Dispose();
            _buildCts            = null;
            _buildBtn.Text       = "Build Image";
            _buildBtn.BackColor  = AppTheme.Accent;
            _progressBar.Visible = false;
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────
    private bool ValidateInputs(out NodeConfig config, out string baseImage, out string outputPath)
    {
        config     = new NodeConfig();
        baseImage  = _baseImageBox.Text.Trim();
        outputPath = "";

        if (string.IsNullOrEmpty(baseImage) || !File.Exists(baseImage))
        {
            MessageBox.Show("Please select a valid base .img file.", "Base Image Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var outDir = _outputDirBox.Text.Trim();
        if (string.IsNullOrEmpty(outDir) || !Directory.Exists(outDir))
        {
            MessageBox.Show("Please select a valid output folder.", "Output Folder Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_hostnameBox.Text))
        {
            MessageBox.Show("Hostname cannot be empty.", "Hostname Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_usernameBox.Text))
        {
            MessageBox.Show("Username cannot be empty.", "Username Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!string.IsNullOrEmpty(_passwordBox.Text) && _passwordBox.Text != _passwordConfBox.Text)
        {
            MessageBox.Show("Passwords do not match.", "Password Mismatch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        config.Hostname         = _hostnameBox.Text.Trim();
        config.Username         = _usernameBox.Text.Trim();
        config.Password         = _passwordBox.Text;
        config.Timezone         = _timezoneBox.Text.Trim();
        config.InstallDocker    = _dockerCheck.Checked;
        config.InstallK8s       = _k8sCheck.Checked;
        config.InstallPortainer = _portainerCheck.Checked;
        config.ExtraPackages    = _extraPkgBox.Text.Trim();
        config.ExtraRuncmds     = _extraCmdBox.Text.Trim();

        var baseName = Path.GetFileNameWithoutExtension(baseImage);
        outputPath = Path.Combine(outDir, $"{baseName}-{config.Hostname}.img");
        return true;
    }

    // ── Browse dialogs ────────────────────────────────────────────────────────
    private void BrowseBaseImage(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select base Ubuntu image",
            Filter = "Disk images (*.img;*.img.xz)|*.img;*.img.xz|All files (*.*)|*.*",
        };
        if (!string.IsNullOrEmpty(_baseImageBox.Text))
            dlg.InitialDirectory = Path.GetDirectoryName(_baseImageBox.Text) ?? "";
        if (dlg.ShowDialog() == DialogResult.OK)
            _baseImageBox.Text = dlg.FileName;
    }

    private void BrowseOutputDir(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description        = "Select output folder for the built image",
            UseDescriptionForTitle = true,
            InitialDirectory   = _outputDirBox.Text,
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _outputDirBox.Text = dlg.SelectedPath;
    }

    private void OpenRockchipBrowser(object? s, EventArgs e)
    {
        using var dlg = new RockchipBrowserForm();
        dlg.PresetSaveDir = !string.IsNullOrEmpty(_outputDirBox.Text)
            ? _outputDirBox.Text
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.DownloadedImagePath != null)
            _baseImageBox.Text = dlg.DownloadedImagePath;
    }

    // ── Password strength ─────────────────────────────────────────────────────
    private void OnPasswordChanged(object? s, EventArgs e)
    {
        var pw   = _passwordBox.Text;
        var conf = _passwordConfBox.Text;

        if (string.IsNullOrEmpty(pw))
        {
            _passwordStrLbl.Text      = "Password hashed automatically at build time";
            _passwordStrLbl.ForeColor = AppTheme.TextMuted;
            return;
        }

        if (!string.IsNullOrEmpty(conf) && pw != conf)
        {
            _passwordStrLbl.Text      = "Passwords do not match";
            _passwordStrLbl.ForeColor = AppTheme.Danger;
            return;
        }

        int strength = 0;
        if (pw.Length >= 8)  strength++;
        if (pw.Length >= 12) strength++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) strength++;
        if (pw.Any(char.IsDigit)) strength++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) strength++;

        (_passwordStrLbl.Text, _passwordStrLbl.ForeColor) = strength switch
        {
            <= 1 => ("Strength: Weak",   AppTheme.Danger),
            2    => ("Strength: Fair",   AppTheme.Warning),
            3    => ("Strength: Good",   Color.FromArgb(180, 180, 60)),
            _    => ("Strength: Strong", AppTheme.Success),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void SwitchToOutput()
    {
        // No-op in single-form layout — output is always visible
    }

    private void Log(string message)
    {
        if (_outputBox.InvokeRequired) { _outputBox.Invoke(() => Log(message)); return; }
        _outputBox.AppendText(message + "\n");
        _outputBox.ScrollToCaret();
    }

    private void SetStatus(string message)
    {
        if (_statusLabel.InvokeRequired) { _statusLabel.Invoke(() => SetStatus(message)); return; }
        _statusLabel.Text = message;
    }

    private static Label FieldLabel(string text, int x, int y, int w) =>
        new()
        {
            Text      = text,
            Location  = new Point(x, y + 4),
            Width     = w,
            TextAlign = ContentAlignment.MiddleRight,
            Font      = AppTheme.FontSmall,
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
        };

    private static Button AccentBtn(string text, int x, int y, int w, int h) =>
        new()
        {
            Text      = text,
            Location  = new Point(x, y),
            Width     = w,
            Height    = h,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.Accent,
            Font      = AppTheme.FontSmall,
            Cursor    = Cursors.Hand,
        };

    // ── Settings persistence ──────────────────────────────────────────────────
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(
                new AppSettings
                {
                    BaseImagePath     = _baseImageBox.Text,
                    OutputDir         = _outputDirBox.Text,
                    WslDistro         = _wslDistroBox.Text,
                    WeeklyUpdatesOnly = _weeklyUpdateCheck?.Checked ?? false,
                    LastUpdateCheck   = _lastUpdateCheck,
                    LastConfig        = new NodeConfig
                    {
                        Hostname         = _hostnameBox.Text,
                        Username         = _usernameBox.Text,
                        Timezone         = _timezoneBox.Text,
                        InstallDocker    = _dockerCheck.Checked,
                        InstallK8s       = _k8sCheck.Checked,
                        InstallPortainer = _portainerCheck.Checked,
                        ExtraPackages    = _extraPkgBox.Text,
                        ExtraRuncmds     = _extraCmdBox.Text,
                    },
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ignore */ }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
            if (s == null) return;

            if (!string.IsNullOrEmpty(s.BaseImagePath)) _baseImageBox.Text = s.BaseImagePath;
            if (!string.IsNullOrEmpty(s.OutputDir))     _outputDirBox.Text = s.OutputDir;
            if (!string.IsNullOrEmpty(s.WslDistro))     _wslDistroBox.Text = s.WslDistro;
            _weeklyUpdateCheck.Checked = s.WeeklyUpdatesOnly;
            _lastUpdateCheck           = s.LastUpdateCheck;

            if (s.LastConfig is { } c)
            {
                _hostnameBox.Text       = c.Hostname;
                _usernameBox.Text       = c.Username;
                _timezoneBox.Text       = c.Timezone;
                _dockerCheck.Checked    = c.InstallDocker;
                _k8sCheck.Checked       = c.InstallK8s;
                _portainerCheck.Checked = c.InstallPortainer;
                _portainerCheck.Enabled = c.InstallDocker;
                _extraPkgBox.Text       = c.ExtraPackages;
                _extraCmdBox.Text       = c.ExtraRuncmds;
            }
        }
        catch { /* ignore */ }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }
}

// ── Themed controls ───────────────────────────────────────────────────────────
internal class DarkTextBox : TextBox
{
    public DarkTextBox()
    {
        BackColor   = AppTheme.BgInput;
        ForeColor   = AppTheme.TextPrimary;
        BorderStyle = BorderStyle.FixedSingle;
        Font        = AppTheme.FontBody;
        Height      = 26;
    }
}

internal class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        ForeColor = AppTheme.TextSecondary;
        BackColor = Color.Transparent;
        Font      = AppTheme.FontBody;
    }
}
