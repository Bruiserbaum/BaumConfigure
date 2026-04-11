using System.Text.Json;
using BaumConfigureGUI.Models;
using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

public class MainForm : Form
{
    // ── Setup tab controls ────────────────────────────────────────────────────
    private DarkTextBox   _baseImageBox    = null!;
    private DarkTextBox   _outputDirBox    = null!;
    private DarkTextBox   _wslDistroBox    = null!;
    private DarkTextBox   _hostnameBox     = null!;
    private DarkTextBox   _usernameBox     = null!;
    private DarkTextBox   _passwordBox     = null!;
    private DarkTextBox   _passwordConfBox = null!;
    private Label         _passwordStrLbl  = null!;
    private DarkTextBox   _timezoneBox     = null!;
    private DarkCheckBox  _sshPwdCheck     = null!;
    private DarkCheckBox  _dockerCheck     = null!;
    private DarkCheckBox  _k8sCheck        = null!;
    private DarkCheckBox  _portainerCheck  = null!;
    private DarkTextBox   _extraPkgBox     = null!;

    // ── Advanced tab controls ─────────────────────────────────────────────────
    private DarkCheckBox  _configNetCheck  = null!;
    private RadioButton   _ethernetRadio   = null!;
    private RadioButton   _wifiRadio       = null!;
    private DarkCheckBox  _dhcpCheck       = null!;
    private DarkTextBox   _staticIpBox     = null!;
    private DarkTextBox   _gatewayBox      = null!;
    private DarkTextBox   _dnsBox          = null!;
    private DarkTextBox   _wifiSsidBox     = null!;
    private DarkTextBox   _wifiPassBox     = null!;
    private DarkCheckBox  _autoPatchCheck  = null!;
    private DarkCheckBox  _logRotCheck     = null!;
    private DarkCheckBox  _weeklyRebootCheck = null!;
    private ComboBox      _dockerRestartCombo = null!;
    private DarkTextBox   _extraCmdBox     = null!;

    // ── Status / output ───────────────────────────────────────────────────────
    private RichTextBox   _outputBox       = null!;
    private Button        _buildBtn        = null!;
    private Label         _statusLabel     = null!;
    private ProgressBar   _progressBar     = null!;
    private Label         _updateBadge     = null!;
    private ReleaseInfo?  _pendingUpdate   = null;

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
        Size          = new Size(1080, 900);
        MinimumSize   = new Size(880, 820);
        StartPosition = FormStartPosition.CenterScreen;
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
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(0), BackColor = AppTheme.BgMain,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.Controls.Add(BuildTitleBar(),  0, 0);
        root.Controls.Add(BuildBody(),      0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);

        Controls.Add(root);
    }

    // ── Title bar ─────────────────────────────────────────────────────────────
    private Panel BuildTitleBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep,
            Padding = new Padding(16, 0, 16, 0),
        };

        panel.Controls.Add(new Label
        {
            Text = "BaumConfigure", Dock = DockStyle.Left, Width = 220,
            TextAlign = ContentAlignment.MiddleLeft, Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent,
        });
        panel.Controls.Add(new Label
        {
            Text = "OS Image Builder", Dock = DockStyle.Left, Width = 150,
            TextAlign = ContentAlignment.MiddleLeft, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted, BackColor = Color.Transparent,
        });

        var ver = new Label
        {
            Text = $"v{UpdateService.CurrentVersion}", Dock = DockStyle.Right, Width = 60,
            TextAlign = ContentAlignment.MiddleRight, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted, BackColor = Color.Transparent,
        };

        _updateBadge = new Label
        {
            Text = "Update available", Dock = DockStyle.Right, Width = 110,
            TextAlign = ContentAlignment.MiddleCenter, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.BgMain, BackColor = AppTheme.Warning,
            Cursor = Cursors.Hand, Visible = false,
        };
        _updateBadge.Click += OnUpdateBadgeClicked;

        var checkUpdBtn = new Button
        {
            Text = "Check Updates", Dock = DockStyle.Right, Width = 108,
            FlatStyle = FlatStyle.Flat, BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.TextSecondary, Font = AppTheme.FontSmall,
            Cursor = Cursors.Hand,
        };
        checkUpdBtn.FlatAppearance.BorderColor = AppTheme.Border;
        checkUpdBtn.FlatAppearance.BorderSize  = 1;
        checkUpdBtn.Click += OnCheckUpdateClicked;

        panel.Controls.AddRange([ver, _updateBadge, checkUpdBtn]);
        return panel;
    }

    // ── Body ──────────────────────────────────────────────────────────────────
    private Control BuildBody()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterWidth = 4, BackColor = AppTheme.Border,
        };
        split.Panel1.BackColor = AppTheme.BgMain;
        split.Panel2.BackColor = AppTheme.BgMain;

        Load += (_, _) =>
        {
            split.Panel1MinSize = 480;
            split.Panel2MinSize = 320;
            try { split.SplitterDistance = 520; } catch { }
        };

        split.Panel1.Controls.Add(BuildConfigPanel());
        split.Panel2.Controls.Add(BuildOutputPanel());
        return split;
    }

    // ── Config panel (custom tabs — no TabControl, full dark theme) ──────────
    private Panel _setupContent  = null!;
    private Panel _advContent    = null!;
    private Button _setupTabBtn  = null!;
    private Button _advTabBtn    = null!;

    private Control BuildConfigPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgDeep };

        // ── Build button pinned at bottom ─────────────────────────────────────
        var buildArea = new Panel
        {
            Dock = DockStyle.Bottom, Height = 58,
            BackColor = AppTheme.BgMain, Padding = new Padding(18, 8, 18, 8),
        };
        buildArea.Controls.Add(new Panel
            { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.Border });
        _buildBtn = new Button
        {
            Text = "Build Image", Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat, BackColor = AppTheme.Accent,
            ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        _buildBtn.FlatAppearance.BorderSize = 0;
        _buildBtn.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
        _buildBtn.Click += OnBuildClicked;
        buildArea.Controls.Add(_buildBtn);

        // ── Tab header bar ────────────────────────────────────────────────────
        var tabBar = new Panel
        {
            Dock = DockStyle.Top, Height = 36, BackColor = AppTheme.BgDeep,
        };

        _setupTabBtn = MakeTabButton("Setup",    0);
        _advTabBtn   = MakeTabButton("Advanced", 120);
        _setupTabBtn.Click += (_, _) => SelectConfigTab(setup: true);
        _advTabBtn.Click   += (_, _) => SelectConfigTab(setup: false);
        tabBar.Controls.AddRange([_setupTabBtn, _advTabBtn]);

        // ── Content panels ────────────────────────────────────────────────────
        _setupContent = (Panel)BuildSetupTab();
        _advContent   = (Panel)BuildAdvancedTab();

        // Both fill; WinForms excludes hidden controls from layout
        _setupContent.Dock = DockStyle.Fill;
        _advContent.Dock   = DockStyle.Fill;
        _advContent.Visible = false;

        var contentArea = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgMain };
        // Add in reverse order so Fill layout works correctly
        contentArea.Controls.Add(_advContent);
        contentArea.Controls.Add(_setupContent);

        outer.Controls.Add(contentArea);
        outer.Controls.Add(tabBar);
        outer.Controls.Add(buildArea);

        SelectConfigTab(setup: true);
        return outer;
    }

    private static Button MakeTabButton(string text, int x) =>
        new()
        {
            Text      = text,
            Location  = new Point(x, 0),
            Width     = 120,
            Height    = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.TextPrimary,
            Font      = AppTheme.FontHeader,
            Cursor    = Cursors.Hand,
        };

    private void SelectConfigTab(bool setup)
    {
        _setupContent.Visible = setup;
        _advContent.Visible   = !setup;

        _setupTabBtn.BackColor = setup  ? AppTheme.BgCard : AppTheme.BgDeep;
        _setupTabBtn.ForeColor = setup  ? AppTheme.TextPrimary : AppTheme.TextMuted;
        _advTabBtn.BackColor   = !setup ? AppTheme.BgCard : AppTheme.BgDeep;
        _advTabBtn.ForeColor   = !setup ? AppTheme.TextPrimary : AppTheme.TextMuted;

        // Accent underline on selected tab button
        StyleTabAccent(_setupTabBtn, setup);
        StyleTabAccent(_advTabBtn,   !setup);
    }

    private static void StyleTabAccent(Button btn, bool selected)
    {
        btn.FlatAppearance.BorderSize  = selected ? 0 : 0;
        btn.FlatAppearance.BorderColor = AppTheme.BgDeep;
        // Repaint with accent bottom border
        btn.Paint -= DrawTabAccent;
        if (selected) btn.Paint += DrawTabAccent;
        btn.Invalidate();
    }

    private static void DrawTabAccent(object? sender, PaintEventArgs e)
    {
        var btn = (Button)sender!;
        using var b = new SolidBrush(AppTheme.Accent);
        e.Graphics.FillRectangle(b, 0, btn.Height - 3, btn.Width, 3);
    }

    // ── Setup tab ─────────────────────────────────────────────────────────────
    private Control BuildSetupTab()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = AppTheme.BgMain, Padding = new Padding(18, 14, 14, 14),
        };

        const int LW = 128;
        const int FX = 136;
        const int FW = 264;
        int y = 6;

        void Section(string title)
        {
            y += 4;
            scroll.Controls.Add(new Label
            {
                Text = title, Location = new Point(0, y), AutoSize = true,
                Font = AppTheme.FontHeader, ForeColor = AppTheme.Accent, BackColor = Color.Transparent,
            });
            y += 20;
            scroll.Controls.Add(new Panel
            {
                Location = new Point(0, y), Size = new Size(FX + FW, 1),
                BackColor = AppTheme.Border,
            });
            y += 8;
        }

        DarkTextBox Field(string label, string val, bool password = false, string? placeholder = null)
        {
            scroll.Controls.Add(new Label
            {
                Text = label, Location = new Point(0, y + 4), Width = LW,
                TextAlign = ContentAlignment.MiddleRight, Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent,
            });
            var tb = new DarkTextBox { Location = new Point(FX, y), Width = FW, Text = val };
            if (password) tb.UseSystemPasswordChar = true;
            if (placeholder != null) tb.PlaceholderText = placeholder;
            scroll.Controls.Add(tb);
            y += 30;
            return tb;
        }

        DarkCheckBox Check(string label, bool val, int xOff = 0)
        {
            var cb = new DarkCheckBox
                { Text = label, Location = new Point(FX + xOff, y), AutoSize = true, Checked = val };
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

        // Image source buttons
        var rockchipBtn = AccentBtn("Get Rockchip Image", FX, y, FW / 2 - 2, 24);
        rockchipBtn.FlatAppearance.BorderColor = AppTheme.Accent;
        rockchipBtn.FlatAppearance.BorderSize  = 1;
        rockchipBtn.Click += OpenRockchipBrowser;

        var rpiBtn = AccentBtn("Get Raspberry Pi Image", FX + FW / 2 + 2, y, FW / 2 - 2, 24);
        rpiBtn.FlatAppearance.BorderColor = AppTheme.Accent;
        rpiBtn.FlatAppearance.BorderSize  = 1;
        rpiBtn.Click += OpenRaspberryPiBrowser;

        scroll.Controls.AddRange([rockchipBtn, rpiBtn]);
        y += 30;

        scroll.Controls.Add(FieldLabel("Output Folder:", 0, y, LW));
        _outputDirBox = new DarkTextBox { Location = new Point(FX, y), Width = FW - 66, PlaceholderText = "Select output folder…" };
        var browseOut = AccentBtn("Browse…", FX + FW - 60, y, 60, 26);
        browseOut.Click += BrowseOutputDir;
        scroll.Controls.AddRange([_outputDirBox, browseOut]);
        y += 30;

        scroll.Controls.Add(FieldLabel("WSL Distro:", 0, y, LW));
        _wslDistroBox = new DarkTextBox { Location = new Point(FX, y), Width = 110, Text = "Ubuntu" };
        var setupWslBtn = AccentBtn("Install WSL Tools", FX + 116, y, 148, 26);
        setupWslBtn.Click += OnSetupWslClicked;
        scroll.Controls.AddRange([_wslDistroBox, setupWslBtn]);
        y += 34;

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
            Location = new Point(FX, y), AutoSize = true, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted, BackColor = Color.Transparent,
            Text = "Password hashed automatically at build time",
        };
        scroll.Controls.Add(_passwordStrLbl);
        y += 22;

        _sshPwdCheck = Check("Enable SSH password login", true, 0);
        y += 28;

        // ── Software ──────────────────────────────────────────────────────────
        y += 4;
        Section("Software");

        var presetDockerBtn = AccentBtn("Docker Host", FX,           y, 90, 22);
        var presetK8sBtn    = AccentBtn("K8s Node",    FX + 96,      y, 80, 22);
        var presetFullBtn   = AccentBtn("Full Stack",  FX + 182,     y, 80, 22);
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

        return scroll;
    }

    // ── Advanced tab ──────────────────────────────────────────────────────────
    private Control BuildAdvancedTab()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = AppTheme.BgMain, Padding = new Padding(18, 14, 14, 14),
        };

        const int LW = 128;
        const int FX = 136;
        const int FW = 264;
        int y = 6;

        void Section(string title)
        {
            y += 4;
            scroll.Controls.Add(new Label
            {
                Text = title, Location = new Point(0, y), AutoSize = true,
                Font = AppTheme.FontHeader, ForeColor = AppTheme.Accent, BackColor = Color.Transparent,
            });
            y += 20;
            scroll.Controls.Add(new Panel
            {
                Location = new Point(0, y), Size = new Size(FX + FW, 1),
                BackColor = AppTheme.Border,
            });
            y += 8;
        }

        // ── Network ───────────────────────────────────────────────────────────
        Section("Network");

        _configNetCheck = new DarkCheckBox { Text = "Configure Network", Location = new Point(FX, y), AutoSize = true };
        _ethernetRadio  = ThemedRadio("Ethernet", FX + 164, y + 2);
        _wifiRadio      = ThemedRadio("WiFi",     FX + 244, y + 2);
        _ethernetRadio.Checked = true;
        _configNetCheck.CheckedChanged += (_, _) => UpdateNetworkFields();
        _ethernetRadio.CheckedChanged  += (_, _) => UpdateNetworkFields();
        scroll.Controls.AddRange([_configNetCheck, _ethernetRadio, _wifiRadio]);
        y += 28;

        _dhcpCheck   = new DarkCheckBox { Text = "DHCP", Location = new Point(FX, y + 3), AutoSize = true, Checked = true };
        _staticIpBox = new DarkTextBox  { Location = new Point(FX + 68, y), Width = FW - 68, PlaceholderText = "e.g. 192.168.1.10/24" };
        scroll.Controls.Add(FieldLabel("IP / CIDR:", 0, y, LW));
        _dhcpCheck.CheckedChanged += (_, _) => UpdateNetworkFields();
        scroll.Controls.AddRange([_dhcpCheck, _staticIpBox]);
        y += 28;

        scroll.Controls.Add(FieldLabel("Gateway:", 0, y, LW));
        _gatewayBox = new DarkTextBox { Location = new Point(FX, y), Width = FW, PlaceholderText = "e.g. 192.168.1.1" };
        scroll.Controls.Add(_gatewayBox);
        y += 28;

        scroll.Controls.Add(FieldLabel("DNS Servers:", 0, y, LW));
        _dnsBox = new DarkTextBox { Location = new Point(FX, y), Width = FW, Text = "8.8.8.8,8.8.4.4" };
        scroll.Controls.Add(_dnsBox);
        y += 28;

        scroll.Controls.Add(FieldLabel("WiFi SSID:", 0, y, LW));
        _wifiSsidBox = new DarkTextBox { Location = new Point(FX, y), Width = 130, PlaceholderText = "Network name" };
        var wifiPwdLbl = new Label
        {
            Text = "Pwd:", Location = new Point(FX + 136, y + 4), AutoSize = true,
            Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent,
        };
        _wifiPassBox = new DarkTextBox { Location = new Point(FX + 166, y), Width = FW - 166, UseSystemPasswordChar = true };
        scroll.Controls.AddRange([_wifiSsidBox, wifiPwdLbl, _wifiPassBox]);
        y += 30;

        UpdateNetworkFields();

        // ── Tweaks ────────────────────────────────────────────────────────────
        y += 4;
        Section("Tweaks");

        // Row 1: auto-patch + log rotation
        _autoPatchCheck = new DarkCheckBox { Text = "Auto-patch (unattended-upgrades)", Location = new Point(FX, y), AutoSize = true, Checked = true };
        scroll.Controls.Add(_autoPatchCheck);
        y += 26;

        _logRotCheck = new DarkCheckBox { Text = "Log rotation (journalctl vacuum + logrotate weekly)", Location = new Point(FX, y), AutoSize = true, Checked = true };
        scroll.Controls.Add(_logRotCheck);
        y += 26;

        _weeklyRebootCheck = new DarkCheckBox { Text = "Weekly reboot (Sunday 2 am via cron)", Location = new Point(FX, y), AutoSize = true, Checked = false };
        scroll.Controls.Add(_weeklyRebootCheck);
        y += 28;

        // Docker restart policy
        scroll.Controls.Add(FieldLabel("Docker Restart:", 0, y, LW));
        _dockerRestartCombo = new ComboBox
        {
            Location      = new Point(FX, y),
            Width         = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = AppTheme.BgInput,
            ForeColor     = AppTheme.TextPrimary,
            FlatStyle     = FlatStyle.Flat,
            Font          = AppTheme.FontBody,
        };
        _dockerRestartCombo.Items.AddRange(["no", "unless-stopped", "always", "on-failure"]);
        _dockerRestartCombo.SelectedItem = "unless-stopped";
        scroll.Controls.Add(_dockerRestartCombo);
        y += 32;

        // ── First-Boot Commands ───────────────────────────────────────────────
        y += 4;
        Section("First-Boot Commands");

        scroll.Controls.Add(FieldLabel("Commands:", 0, y, LW));
        _extraCmdBox = new DarkTextBox
        {
            Location = new Point(FX, y), Width = FW, Height = 80,
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "One command per line",
        };
        scroll.Controls.Add(_extraCmdBox);
        y += 88;

        return scroll;
    }

    // ── Output panel ──────────────────────────────────────────────────────────
    private Control BuildOutputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            BackColor = AppTheme.BgMain, Padding = new Padding(6, 8, 10, 10),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BgMain };
        var outLbl = new Label
        {
            Text = "Build Output", Dock = DockStyle.Left, Width = 120,
            TextAlign = ContentAlignment.MiddleLeft, Font = AppTheme.FontHeader,
            ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent,
        };
        var clearBtn = new Button
        {
            Text = "Clear", Dock = DockStyle.Right, Width = 56, FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.BgCard, ForeColor = AppTheme.TextSecondary, Font = AppTheme.FontSmall,
        };
        clearBtn.FlatAppearance.BorderColor = AppTheme.Border;
        clearBtn.Click += (_, _) => _outputBox.Clear();
        header.Controls.AddRange([outLbl, clearBtn]);

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BackColor = AppTheme.BgDeep,
            ForeColor = Color.FromArgb(180, 220, 130), Font = AppTheme.FontMono,
            WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both,
            BorderStyle = BorderStyle.None,
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 0,
            Style = ProgressBarStyle.Continuous, Visible = false,
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
            Dock = DockStyle.Fill, ForeColor = AppTheme.TextMuted, BackColor = Color.Transparent,
            Font = AppTheme.FontSmall, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Ready — select a base image, configure your settings, then click Build Image.",
        };
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    // ── Update logic ──────────────────────────────────────────────────────────
    private async Task CheckForUpdateAsync(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _lastUpdateCheck).TotalHours < 1) return;

        try
        {
            var info = await UpdateService.CheckAsync();
            _lastUpdateCheck = DateTime.UtcNow;
            SaveSettings();
            if (info is null) return;
            _pendingUpdate = info;
            if (_updateBadge.InvokeRequired) _updateBadge.Invoke(() => _updateBadge.Visible = true);
            else _updateBadge.Visible = true;
        }
        catch { }
    }

    private async void OnCheckUpdateClicked(object? s, EventArgs e)
    {
        SetStatus("Checking for updates…");
        await CheckForUpdateAsync(force: true);
        if (_pendingUpdate is null) SetStatus("You are up to date.");
    }

    private void OnUpdateBadgeClicked(object? s, EventArgs e)
    {
        if (_pendingUpdate is null) return;
        var result = MessageBox.Show(
            $"BaumConfigure v{_pendingUpdate.Version} is available.\n\n" +
            "The update will be downloaded and installed automatically.\n" +
            "The app will restart when complete.\n\nInstall now?",
            "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (result == DialogResult.Yes)
            _ = InstallUpdateAsync(_pendingUpdate);
    }

    private async Task InstallUpdateAsync(ReleaseInfo info)
    {
        _buildBtn.Enabled    = false;
        _updateBadge.Visible = false;
        _progressBar.Visible = true;
        SetStatus("Downloading update…");
        Log($"\n── Updating to BaumConfigure v{info.Version} ──");

        try
        {
            await UpdateService.DownloadAndInstallAsync(info,
                pct =>
                {
                    if (_progressBar.InvokeRequired) _progressBar.Invoke(() => _progressBar.Value = pct);
                    else _progressBar.Value = pct;
                    if (pct % 10 == 0) Log($"  Download: {pct}%");
                }, Log);
        }
        catch (Exception ex)
        {
            Log($"✘ Update failed: {ex.Message}");
            SetStatus("Update failed — see output.");
            _buildBtn.Enabled    = true;
            _progressBar.Visible = false;
        }
    }

    // ── WSL tool setup ────────────────────────────────────────────────────────
    private async void OnSetupWslClicked(object? s, EventArgs e)
    {
        var btn = (Button)s!;
        btn.Enabled = false;
        var distro = _wslDistroBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(distro)) distro = "Ubuntu";

        _outputBox.Clear();
        Log($"── Installing build tools in WSL ({distro})…");
        Log("   Packages: whois xz-utils");
        Log("");
        SetStatus("Installing WSL tools…");

        try
        {
            // Run as root to avoid interactive sudo prompts in non-TTY WSL sessions.
            // DEBIAN_FRONTEND=noninteractive prevents apt from asking any questions.
            var wsl = new WslService(distro);
            await wsl.RunAsync(
                "DEBIAN_FRONTEND=noninteractive apt-get update -y && " +
                "DEBIAN_FRONTEND=noninteractive apt-get install -y whois xz-utils",
                Log, user: "root");
            Log("");
            Log("✔ WSL tools installed. You can now build images.");
            SetStatus("WSL tools ready.");
        }
        catch (Exception ex)
        {
            Log($"✘ {ex.Message}");
            Log("");
            Log("Run manually in WSL:");
            Log("  sudo apt-get install -y libguestfs-tools whois");
            SetStatus("WSL tool install failed — see output.");
        }
        finally { btn.Enabled = true; }
    }

    // ── Build logic ───────────────────────────────────────────────────────────
    private async void OnBuildClicked(object? s, EventArgs e)
    {
        if (_buildCts != null) { _buildCts.Cancel(); return; }

        if (!ValidateInputs(out var config, out var baseImage, out var outputPath)) return;

        _buildBtn.Text       = "Cancel";
        _buildBtn.BackColor  = AppTheme.Danger;
        _progressBar.Value   = 0;
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
            Log($"   Network : {(config.ConfigureNetwork ? config.NetworkType + (config.UseDhcp ? "/DHCP" : "/static") : "default")}");
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
        catch (OperationCanceledException) { Log("── Build cancelled."); SetStatus("Cancelled."); }
        catch (Exception ex)               { Log($"✘ Error: {ex.Message}"); SetStatus("Build failed — see output."); }
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
        config    = new NodeConfig();
        baseImage = _baseImageBox.Text.Trim();
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
        config.SshPasswordAuth  = _sshPwdCheck.Checked;
        config.InstallDocker    = _dockerCheck.Checked;
        config.InstallK8s       = _k8sCheck.Checked;
        config.InstallPortainer = _portainerCheck.Checked;
        config.ExtraPackages    = _extraPkgBox.Text.Trim();
        config.ExtraRuncmds     = _extraCmdBox.Text.Trim();

        config.AutoPatch           = _autoPatchCheck.Checked;
        config.LogRotation         = _logRotCheck.Checked;
        config.WeeklyReboot        = _weeklyRebootCheck.Checked;
        config.DockerRestartPolicy = _dockerRestartCombo.SelectedItem?.ToString() ?? "unless-stopped";

        config.ConfigureNetwork = _configNetCheck.Checked;
        config.NetworkType      = _wifiRadio.Checked ? "wifi" : "ethernet";
        config.UseDhcp          = _dhcpCheck.Checked;
        config.StaticIp         = _staticIpBox.Text.Trim();
        config.Gateway          = _gatewayBox.Text.Trim();
        config.DnsServers       = _dnsBox.Text.Trim();
        config.WifiSsid         = _wifiSsidBox.Text.Trim();
        config.WifiPassword     = _wifiPassBox.Text;

        // Strip compression extension first (.xz, .gz, .bz2, .zst), then .img
        var stripped = baseImage;
        foreach (var cext in new[] { ".xz", ".gz", ".bz2", ".zst" })
            if (stripped.EndsWith(cext, StringComparison.OrdinalIgnoreCase))
                stripped = stripped[..^cext.Length];
        var baseName = Path.GetFileNameWithoutExtension(stripped);
        outputPath = Path.Combine(outDir, $"{baseName}-{config.Hostname}.img");
        return true;
    }

    // ── Browse dialogs ────────────────────────────────────────────────────────
    private void BrowseBaseImage(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select base Ubuntu image",
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
            Description = "Select output folder for the built image",
            UseDescriptionForTitle = true, InitialDirectory = _outputDirBox.Text,
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

    private void OpenRaspberryPiBrowser(object? s, EventArgs e)
    {
        using var dlg = new RaspberryPiBrowserForm();
        dlg.PresetSaveDir = !string.IsNullOrEmpty(_outputDirBox.Text)
            ? _outputDirBox.Text
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.DownloadedImagePath != null)
            _baseImageBox.Text = dlg.DownloadedImagePath;
    }

    // ── Network field enable/disable ──────────────────────────────────────────
    private void UpdateNetworkFields()
    {
        bool active = _configNetCheck.Checked;
        bool dhcp   = _dhcpCheck.Checked;
        bool wifi   = _wifiRadio.Checked;

        _ethernetRadio.Enabled = active;
        _wifiRadio.Enabled     = active;
        _dhcpCheck.Enabled     = active;
        _staticIpBox.Enabled   = active && !dhcp;
        _gatewayBox.Enabled    = active && !dhcp;
        _dnsBox.Enabled        = active && !dhcp;
        _wifiSsidBox.Enabled   = active && wifi;
        _wifiPassBox.Enabled   = active && wifi;
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
            Text = text, Location = new Point(x, y + 4), Width = w,
            TextAlign = ContentAlignment.MiddleRight, Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent,
        };

    private static Button AccentBtn(string text, int x, int y, int w, int h) =>
        new()
        {
            Text = text, Location = new Point(x, y), Width = w, Height = h,
            FlatStyle = FlatStyle.Flat, BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.Accent, Font = AppTheme.FontSmall, Cursor = Cursors.Hand,
        };

    private static RadioButton ThemedRadio(string text, int x, int y) =>
        new()
        {
            Text = text, Location = new Point(x, y), AutoSize = true,
            ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent, Font = AppTheme.FontBody,
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
                    BaseImagePath   = _baseImageBox?.Text ?? "",
                    OutputDir       = _outputDirBox?.Text ?? "",
                    WslDistro       = _wslDistroBox?.Text ?? "Ubuntu",
                    LastUpdateCheck = _lastUpdateCheck,
                    LastConfig      = new NodeConfig
                    {
                        Hostname              = _hostnameBox?.Text ?? "",
                        Username              = _usernameBox?.Text ?? "",
                        Timezone              = _timezoneBox?.Text ?? "",
                        InstallDocker         = _dockerCheck?.Checked ?? false,
                        InstallK8s            = _k8sCheck?.Checked ?? false,
                        InstallPortainer      = _portainerCheck?.Checked ?? false,
                        ExtraPackages         = _extraPkgBox?.Text ?? "",
                        ExtraRuncmds          = _extraCmdBox?.Text ?? "",
                        AutoPatch             = _autoPatchCheck?.Checked ?? true,
                        LogRotation           = _logRotCheck?.Checked ?? true,
                        WeeklyReboot          = _weeklyRebootCheck?.Checked ?? false,
                        DockerRestartPolicy   = _dockerRestartCombo?.SelectedItem?.ToString() ?? "unless-stopped",
                        ConfigureNetwork      = _configNetCheck?.Checked ?? false,
                        NetworkType           = _wifiRadio?.Checked == true ? "wifi" : "ethernet",
                        UseDhcp               = _dhcpCheck?.Checked ?? true,
                        StaticIp              = _staticIpBox?.Text ?? "",
                        Gateway               = _gatewayBox?.Text ?? "",
                        DnsServers            = _dnsBox?.Text ?? "8.8.8.8,8.8.4.4",
                        WifiSsid              = _wifiSsidBox?.Text ?? "",
                        WifiPassword          = _wifiPassBox?.Text ?? "",
                    },
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
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
            _lastUpdateCheck = s.LastUpdateCheck;

            if (s.LastConfig is { } c)
            {
                _hostnameBox.Text       = c.Hostname;
                _usernameBox.Text       = c.Username;
                _timezoneBox.Text       = c.Timezone;
                _sshPwdCheck.Checked    = c.SshPasswordAuth;
                _dockerCheck.Checked    = c.InstallDocker;
                _k8sCheck.Checked       = c.InstallK8s;
                _portainerCheck.Checked = c.InstallPortainer;
                _portainerCheck.Enabled = c.InstallDocker;
                _extraPkgBox.Text       = c.ExtraPackages;
                _extraCmdBox.Text       = c.ExtraRuncmds;

                _autoPatchCheck.Checked          = c.AutoPatch;
                _logRotCheck.Checked             = c.LogRotation;
                _weeklyRebootCheck.Checked       = c.WeeklyReboot;
                if (_dockerRestartCombo.Items.Contains(c.DockerRestartPolicy))
                    _dockerRestartCombo.SelectedItem = c.DockerRestartPolicy;

                _configNetCheck.Checked = c.ConfigureNetwork;
                _wifiRadio.Checked      = c.NetworkType == "wifi";
                _ethernetRadio.Checked  = c.NetworkType != "wifi";
                _dhcpCheck.Checked      = c.UseDhcp;
                if (!string.IsNullOrEmpty(c.StaticIp))    _staticIpBox.Text  = c.StaticIp;
                if (!string.IsNullOrEmpty(c.Gateway))     _gatewayBox.Text   = c.Gateway;
                if (!string.IsNullOrEmpty(c.DnsServers))  _dnsBox.Text       = c.DnsServers;
                if (!string.IsNullOrEmpty(c.WifiSsid))    _wifiSsidBox.Text  = c.WifiSsid;
                if (!string.IsNullOrEmpty(c.WifiPassword)) _wifiPassBox.Text = c.WifiPassword;
                UpdateNetworkFields();
            }
        }
        catch { }
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
        BackColor = AppTheme.BgInput; ForeColor = AppTheme.TextPrimary;
        BorderStyle = BorderStyle.FixedSingle; Font = AppTheme.FontBody; Height = 26;
    }
}

internal class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        ForeColor = AppTheme.TextSecondary; BackColor = Color.Transparent; Font = AppTheme.FontBody;
    }
}
