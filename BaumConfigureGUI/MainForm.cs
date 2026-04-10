using System.Text.Json;
using BaumConfigureGUI.Models;
using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

public class MainForm : Form
{
    // ── Configuration controls ────────────────────────────────────────────────
    private TextBox   _hostnameBox     = null!;
    private TextBox   _usernameBox     = null!;
    private TextBox   _passwordBox     = null!;
    private TextBox   _passwordConfBox = null!;
    private Label     _passwordStrLbl  = null!;
    private TextBox   _timezoneBox     = null!;
    private CheckBox  _dockerCheck     = null!;
    private CheckBox  _k8sCheck        = null!;
    private CheckBox  _portainerCheck  = null!;
    private TextBox   _extraPkgBox     = null!;
    private TextBox   _extraCmdBox     = null!;

    // ── Path controls ─────────────────────────────────────────────────────────
    private TextBox _baseImageBox  = null!;
    private TextBox _outputDirBox  = null!;
    private TextBox _wslDistroBox  = null!;

    // ── Output ────────────────────────────────────────────────────────────────
    private RichTextBox _outputBox   = null!;
    private Button      _buildBtn    = null!;
    private Label       _statusLabel = null!;

    private CancellationTokenSource? _buildCts;

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BaumConfigure", "settings.json");

    // ═════════════════════════════════════════════════════════════════════════
    public MainForm()
    {
        BuildUi();
        LoadSettings();
    }

    // ── UI construction ───────────────────────────────────────────────────────
    private void BuildUi()
    {
        Text          = "BaumConfigure — OS Image Builder";
        Size          = new Size(1060, 740);
        MinimumSize   = new Size(860, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = Color.FromArgb(245, 246, 248);

        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 2,
            ColumnCount = 1,
            Padding     = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        root.Controls.Add(BuildBody(),     0, 0);
        root.Controls.Add(BuildStatusBar(), 0, 1);

        Controls.Add(root);
    }

    private Control BuildBody()
    {
        var split = new SplitContainer
        {
            Dock           = DockStyle.Fill,
            Orientation    = Orientation.Vertical,
            SplitterWidth  = 6,
            SplitterDistance = 430,
            Panel1MinSize  = 360,
            Panel2MinSize  = 320,
        };

        split.Panel1.Controls.Add(BuildConfigPanel());
        split.Panel2.Controls.Add(BuildOutputPanel());
        return split;
    }

    // ── Left panel: configuration ─────────────────────────────────────────────
    private Control BuildConfigPanel()
    {
        var outer = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            Padding   = new Padding(16, 12, 12, 12),
        };

        const int LW = 130;
        const int FX = 138;
        const int FW = 250;
        int y = 4;

        void Section(string title)
        {
            var lbl = new Label
            {
                Text      = title,
                Location  = new Point(0, y),
                AutoSize  = true,
                Font      = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 92, 184),
            };
            outer.Controls.Add(lbl);
            y += 20;
            outer.Controls.Add(new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(FX + FW + 20, 1),
                BackColor = Color.FromArgb(0, 92, 184),
            });
            y += 7;
        }

        TextBox Field(string label, string val, bool password = false)
        {
            outer.Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(0, y + 3),
                Width     = LW,
                TextAlign = ContentAlignment.MiddleRight,
            });
            var tb = new TextBox { Location = new Point(FX, y), Width = FW, Text = val };
            if (password) tb.UseSystemPasswordChar = true;
            outer.Controls.Add(tb);
            y += 30;
            return tb;
        }

        // ── Image ─────────────────────────────────────────────────────────────
        Section("Image");

        outer.Controls.Add(new Label { Text = "Base Image:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _baseImageBox = new TextBox { Location = new Point(FX, y), Width = FW - 60, PlaceholderText = "Select base .img file…" };
        var browseImg = SmallBtn("Browse…", FX + FW - 54, y);
        browseImg.Click += BrowseBaseImage;
        outer.Controls.AddRange([_baseImageBox, browseImg]);
        y += 30;

        outer.Controls.Add(new Label { Text = "Output Folder:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _outputDirBox = new TextBox { Location = new Point(FX, y), Width = FW - 60, PlaceholderText = "Select output folder…" };
        var browseOut = SmallBtn("Browse…", FX + FW - 54, y);
        browseOut.Click += BrowseOutputDir;
        outer.Controls.AddRange([_outputDirBox, browseOut]);
        y += 30;

        outer.Controls.Add(new Label { Text = "WSL Distro:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _wslDistroBox = new TextBox { Location = new Point(FX, y), Width = 100, Text = "Ubuntu" };
        outer.Controls.Add(_wslDistroBox);
        y += 36;

        // ── System ────────────────────────────────────────────────────────────
        Section("System");

        _hostnameBox = Field("Hostname:", "ubuntu-server");
        _timezoneBox = Field("Timezone:", "America/New_York");
        outer.Controls.Add(new Label { Text = "e.g. America/Chicago", Location = new Point(FX + FW + 4, y - 26), AutoSize = true, ForeColor = Color.Gray, Font = new Font(Font.FontFamily, 7.5f) });
        y += 2;

        // ── User Account ──────────────────────────────────────────────────────
        Section("User Account");

        _usernameBox = Field("Username:", "ubuntu");

        // Password row
        outer.Controls.Add(new Label { Text = "Password:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _passwordBox = new TextBox { Location = new Point(FX, y), Width = FW, UseSystemPasswordChar = true };
        _passwordBox.TextChanged += OnPasswordChanged;
        outer.Controls.Add(_passwordBox);
        y += 30;

        outer.Controls.Add(new Label { Text = "Confirm:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _passwordConfBox = new TextBox { Location = new Point(FX, y), Width = FW, UseSystemPasswordChar = true };
        _passwordConfBox.TextChanged += OnPasswordChanged;
        outer.Controls.Add(_passwordConfBox);
        y += 28;

        _passwordStrLbl = new Label
        {
            Location  = new Point(FX, y),
            AutoSize  = true,
            ForeColor = Color.Gray,
            Font      = new Font(Font.FontFamily, 7.5f),
            Text      = "Password will be hashed automatically on build",
        };
        outer.Controls.Add(_passwordStrLbl);
        y += 22;

        // ── Software ──────────────────────────────────────────────────────────
        y += 4;
        Section("Software");

        _dockerCheck = new CheckBox { Text = "Docker", Location = new Point(FX, y), AutoSize = true };
        _dockerCheck.CheckedChanged += (_, _) =>
        {
            if (_portainerCheck != null)
                _portainerCheck.Enabled = _dockerCheck.Checked;
            if (!_dockerCheck.Checked && _portainerCheck != null)
                _portainerCheck.Checked = false;
        };
        outer.Controls.Add(_dockerCheck);

        _k8sCheck = new CheckBox { Text = "Kubernetes (kubeadm)", Location = new Point(FX + 80, y), AutoSize = true };
        outer.Controls.Add(_k8sCheck);
        y += 28;

        _portainerCheck = new CheckBox { Text = "Portainer CE (requires Docker)", Location = new Point(FX, y), AutoSize = true, Enabled = false };
        outer.Controls.Add(_portainerCheck);
        y += 30;

        outer.Controls.Add(new Label { Text = "Extra Packages:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _extraPkgBox = new TextBox { Location = new Point(FX, y), Width = FW, PlaceholderText = "space or comma separated" };
        outer.Controls.Add(_extraPkgBox);
        y += 30;

        // ── Extra Commands ────────────────────────────────────────────────────
        Section("Additional First-Boot Commands");

        outer.Controls.Add(new Label { Text = "Commands:", Location = new Point(0, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        _extraCmdBox = new TextBox { Location = new Point(FX, y), Width = FW, Height = 72, Multiline = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "One command per line" };
        outer.Controls.Add(_extraCmdBox);
        y += 80;

        // ── Build button ──────────────────────────────────────────────────────
        y += 10;
        _buildBtn = new Button
        {
            Text      = "Build Image",
            Location  = new Point(FX, y),
            Width     = FW,
            Height    = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 92, 184),
            ForeColor = Color.White,
            Font      = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        _buildBtn.FlatAppearance.BorderSize = 0;
        _buildBtn.Click += OnBuildClicked;
        outer.Controls.Add(_buildBtn);
        y += 50;

        outer.AutoScrollMinSize = new Size(0, y);
        return outer;
    }

    // ── Right panel: output console ───────────────────────────────────────────
    private Control BuildOutputPanel()
    {
        var panel  = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 8, 8) };
        var header = new Panel { Dock = DockStyle.Top, Height = 34 };

        var title = new Label
        {
            Text      = "Build Output",
            Dock      = DockStyle.Left,
            Width     = 100,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
        };

        var clearBtn = new Button
        {
            Text      = "Clear",
            Dock      = DockStyle.Right,
            Width     = 60,
            FlatStyle = FlatStyle.Flat,
            Height    = 26,
        };
        clearBtn.Click += (_, _) => _outputBox.Clear();

        header.Controls.AddRange([title, clearBtn]);

        _outputBox = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = Color.FromArgb(180, 220, 130),
            Font      = new Font("Consolas", 9f),
            WordWrap  = false,
            ScrollBars = RichTextBoxScrollBars.Both,
        };

        panel.Controls.Add(_outputBox);
        panel.Controls.Add(header);
        return panel;
    }

    private Panel BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 232, 236) };
        _statusLabel = new Label
        {
            Dock      = DockStyle.Fill,
            ForeColor = Color.FromArgb(60, 60, 60),
            Text      = "Ready — select a base image and configure your settings, then click Build Image.",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
        };
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    // ── Event handlers ────────────────────────────────────────────────────────
    private void OnPasswordChanged(object? s, EventArgs e)
    {
        var pw   = _passwordBox.Text;
        var conf = _passwordConfBox.Text;

        if (string.IsNullOrEmpty(pw))
        {
            _passwordStrLbl.Text      = "Password will be hashed automatically on build";
            _passwordStrLbl.ForeColor = Color.Gray;
            return;
        }

        if (!string.IsNullOrEmpty(conf) && pw != conf)
        {
            _passwordStrLbl.Text      = "Passwords do not match";
            _passwordStrLbl.ForeColor = Color.Crimson;
            return;
        }

        // Simple strength indicator
        int strength = 0;
        if (pw.Length >= 8)  strength++;
        if (pw.Length >= 12) strength++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) strength++;
        if (pw.Any(char.IsDigit)) strength++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) strength++;

        (_passwordStrLbl.Text, _passwordStrLbl.ForeColor) = strength switch
        {
            <= 1 => ("Strength: Weak", Color.Crimson),
            2    => ("Strength: Fair", Color.DarkOrange),
            3    => ("Strength: Good", Color.DarkGoldenrod),
            _    => ("Strength: Strong", Color.SeaGreen),
        };
    }

    private async void OnBuildClicked(object? s, EventArgs e)
    {
        if (_buildCts != null)
        {
            // Cancel a running build
            _buildCts.Cancel();
            return;
        }

        if (!ValidateInputs(out var config, out var baseImage, out var outputPath)) return;

        _buildBtn.Text      = "Cancel Build";
        _buildBtn.BackColor = Color.FromArgb(190, 50, 50);
        SetStatus("Building image…");
        _outputBox.Clear();
        _buildCts = new CancellationTokenSource();

        try
        {
            var wsl     = _wslDistroBox.Text.Trim();
            var builder = new ImageBuilderService(wsl);

            // Hash password in WSL if provided
            if (!string.IsNullOrWhiteSpace(_passwordBox.Text))
            {
                Log("── Hashing password…");
                SetStatus("Hashing password via WSL…");
                try
                {
                    config.PasswordHash = await builder.HashPasswordAsync(_passwordBox.Text);
                    Log($"  Password hashed successfully.");
                }
                catch (Exception ex)
                {
                    Log($"✘ Password hashing failed: {ex.Message}");
                    SetStatus("Build failed — see output.");
                    return;
                }
            }

            Log($"── Building image: {outputPath}");
            Log($"   Base: {baseImage}");
            Log($"   Hostname: {config.Hostname}  User: {config.Username}  TZ: {config.Timezone}");
            Log($"   Docker: {config.InstallDocker}  K8s: {config.InstallK8s}  Portainer: {config.InstallPortainer}");
            Log("");

            await builder.BuildImageAsync(config, baseImage, outputPath, Log, _buildCts.Token);

            if (!_buildCts.IsCancellationRequested)
            {
                Log("");
                Log($"✔ Done. Image saved to:");
                Log($"  {outputPath}");
                SetStatus($"Image ready: {Path.GetFileName(outputPath)}");
            }
        }
        catch (OperationCanceledException)
        {
            Log("── Build cancelled.");
            SetStatus("Build cancelled.");
        }
        catch (Exception ex)
        {
            Log($"✘ Build error: {ex.Message}");
            SetStatus("Build failed — see output.");
        }
        finally
        {
            _buildCts?.Dispose();
            _buildCts = null;
            _buildBtn.Text      = "Build Image";
            _buildBtn.BackColor = Color.FromArgb(0, 92, 184);
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

        var outputDir = _outputDirBox.Text.Trim();
        if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
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

        var pw = _passwordBox.Text;
        if (!string.IsNullOrEmpty(pw) && pw != _passwordConfBox.Text)
        {
            MessageBox.Show("Passwords do not match.", "Password Mismatch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        config.Hostname         = _hostnameBox.Text.Trim();
        config.Username         = _usernameBox.Text.Trim();
        config.Password         = pw;
        config.Timezone         = _timezoneBox.Text.Trim();
        config.InstallDocker    = _dockerCheck.Checked;
        config.InstallK8s       = _k8sCheck.Checked;
        config.InstallPortainer = _portainerCheck.Checked;
        config.ExtraPackages    = _extraPkgBox.Text.Trim();
        config.ExtraRuncmds     = _extraCmdBox.Text.Trim();

        var baseName = Path.GetFileNameWithoutExtension(baseImage);
        outputPath = Path.Combine(outputDir, $"{baseName}-{config.Hostname}.img");
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

    private static Button SmallBtn(string text, int x, int y) =>
        new() { Text = text, Location = new Point(x, y), Width = 60, Height = 24, FlatStyle = FlatStyle.Flat };

    // ── Settings persistence ──────────────────────────────────────────────────
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            var s = new AppSettings
            {
                BaseImagePath = _baseImageBox.Text,
                OutputDir     = _outputDirBox.Text,
                WslDistro     = _wslDistroBox.Text,
                LastConfig    = new NodeConfig
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
            };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
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

            if (s.LastConfig is { } c)
            {
                _hostnameBox.Text          = c.Hostname;
                _usernameBox.Text          = c.Username;
                _timezoneBox.Text          = c.Timezone;
                _dockerCheck.Checked       = c.InstallDocker;
                _k8sCheck.Checked          = c.InstallK8s;
                _portainerCheck.Checked    = c.InstallPortainer;
                _portainerCheck.Enabled    = c.InstallDocker;
                _extraPkgBox.Text          = c.ExtraPackages;
                _extraCmdBox.Text          = c.ExtraRuncmds;
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
