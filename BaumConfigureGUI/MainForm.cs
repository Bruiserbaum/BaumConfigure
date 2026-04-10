using System.Text.Json;
using BaumConfigureGUI.Models;
using BaumConfigureGUI.Services;

namespace BaumConfigureGUI;

public class MainForm : Form
{
    // ── Top-bar controls ──────────────────────────────────────────────────────
    private readonly TextBox _repoPathBox  = new();
    private readonly TextBox _wslDistroBox = new();
    private readonly Label   _statusLabel  = new();

    // ── Output console ────────────────────────────────────────────────────────
    private readonly RichTextBox _outputBox = new();
    private TabControl _tabs = null!;

    // ── Node data ─────────────────────────────────────────────────────────────
    private readonly NodeConfig[] _configs =
    [
        new() { NodeNumber = 1, Hostname = "TuringPiRK1",       ImageType = "rk1", InstallGlusterServer = true },
        new() { NodeNumber = 3, Hostname = "TuringPICompute3",  ImageType = "cm4", InstallGlusterServer = true },
        new() { NodeNumber = 4, Hostname = "TuringPICompute4",  ImageType = "cm4", InstallGlusterClient = true },
    ];
    private readonly Dictionary<int, NodeControls> _nodeCtrl = [];

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
        Text            = "BaumConfigure — Turing Pi 2 Node Setup";
        Size            = new Size(980, 780);
        MinimumSize     = new Size(820, 620);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.FromArgb(245, 245, 245);

        var root = new TableLayoutPanel
        {
            Dock      = DockStyle.Fill,
            RowCount  = 3,
            ColumnCount = 1,
            Padding   = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // top bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // tabs
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // status bar

        root.Controls.Add(BuildTopBar(),    0, 0);
        root.Controls.Add(BuildTabs(),      0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);

        Controls.Add(root);
    }

    private Panel BuildTopBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        panel.Controls.Add(Lbl("Repo Path:", 0, 18));

        _repoPathBox.Location = new Point(85, 14);
        _repoPathBox.Width    = 560;
        panel.Controls.Add(_repoPathBox);

        var browse = Btn("Browse…", 652, 13, 80, 24);
        browse.Click += BrowseRepo;
        panel.Controls.Add(browse);

        panel.Controls.Add(Lbl("WSL Distro:", 742, 18));
        _wslDistroBox.Location = new Point(828, 14);
        _wslDistroBox.Width    = 100;
        _wslDistroBox.Text     = "Ubuntu";
        panel.Controls.Add(_wslDistroBox);

        return panel;
    }

    private TabControl BuildTabs()
    {
        _tabs = new TabControl { Dock = DockStyle.Fill };

        foreach (var cfg in _configs)
        {
            var title = cfg.ImageType == "rk1"
                ? $"Node {cfg.NodeNumber}  (RK1)"
                : $"Node {cfg.NodeNumber}  (CM4)";
            var tab = new TabPage(title);
            tab.Controls.Add(BuildNodePanel(cfg));
            _tabs.TabPages.Add(tab);
        }

        // Output tab
        var outTab = new TabPage("Output");
        outTab.Controls.Add(BuildOutputPanel());
        _tabs.TabPages.Add(outTab);

        return _tabs;
    }

    private Panel BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        _statusLabel.Dock      = DockStyle.Fill;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text      = "Ready";
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private Panel BuildOutputPanel()
    {
        var panel  = new Panel { Dock = DockStyle.Fill };
        var header = new Panel { Dock = DockStyle.Top, Height = 32 };

        var clearBtn = Btn("Clear", 0, 4, 70, 24);
        clearBtn.Click += (_, _) => _outputBox.Clear();
        header.Controls.Add(clearBtn);

        _outputBox.Dock      = DockStyle.Fill;
        _outputBox.ReadOnly  = true;
        _outputBox.BackColor = Color.FromArgb(18, 18, 18);
        _outputBox.ForeColor = Color.FromArgb(180, 220, 130);
        _outputBox.Font      = new Font("Consolas", 9f);

        panel.Controls.Add(_outputBox);
        panel.Controls.Add(header);
        return panel;
    }

    // ── Node panel ────────────────────────────────────────────────────────────
    private Panel BuildNodePanel(NodeConfig cfg)
    {
        var scroll = new Panel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            Padding       = new Padding(14, 14, 14, 14),
        };

        var ctrl = new NodeControls();
        _nodeCtrl[cfg.NodeNumber] = ctrl;

        const int LX = 0;
        const int LW = 160;
        const int FX = 166;
        const int FW = 540;
        int y = 10;

        // ─────────────────────────────────────────────────────────────────────
        void Section(string title)
        {
            var lbl = new Label
            {
                Text      = title,
                Location  = new Point(LX, y),
                AutoSize  = true,
                Font      = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
            };
            scroll.Controls.Add(lbl);
            y += 22;

            var sep = new Panel
            {
                Location  = new Point(LX, y),
                Size      = new Size(FX + FW, 1),
                BackColor = Color.FromArgb(0, 102, 204),
            };
            scroll.Controls.Add(sep);
            y += 8;
        }

        TextBox Field(string label, string val, int height = 0)
        {
            scroll.Controls.Add(new Label { Text = label, Location = new Point(LX, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
            var tb = new TextBox { Location = new Point(FX, y), Width = FW, Text = val };
            if (height > 0) { tb.Multiline = true; tb.Height = height; tb.ScrollBars = ScrollBars.Vertical; }
            scroll.Controls.Add(tb);
            y += (height > 0 ? height : 24) + 6;
            return tb;
        }

        CheckBox Check(string label, bool val, int xOffset = 0)
        {
            var cb = new CheckBox { Text = label, Location = new Point(FX + xOffset, y), AutoSize = true, Checked = val };
            scroll.Controls.Add(cb);
            return cb;
        }

        // ── Node ─────────────────────────────────────────────────────────────
        Section("Node");

        ctrl.Hostname = Field("Hostname:", cfg.Hostname);

        scroll.Controls.Add(new Label { Text = "Image Type:", Location = new Point(LX, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        ctrl.ImageType = new ComboBox { Location = new Point(FX, y), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        ctrl.ImageType.Items.AddRange(["rk1", "cm4"]);
        ctrl.ImageType.SelectedItem = cfg.ImageType;
        scroll.Controls.Add(ctrl.ImageType);
        y += 30;

        // ── User ─────────────────────────────────────────────────────────────
        Section("User Account");

        ctrl.Username = Field("Username:", cfg.Username);
        ctrl.SshKey   = Field("SSH Public Key:", cfg.SshPublicKey, height: 56);

        // Password row with helper button
        scroll.Controls.Add(new Label { Text = "Password Hash:", Location = new Point(LX, y + 3), Width = LW, TextAlign = ContentAlignment.MiddleRight });
        ctrl.PasswordHash = new TextBox { Location = new Point(FX, y), Width = FW - 130 };
        scroll.Controls.Add(ctrl.PasswordHash);
        var hashHelp = Btn("How to generate", FX + FW - 124, y, 124, 24);
        hashHelp.Click += (_, _) => MessageBox.Show(
            "Run in WSL or Linux:\n\n  mkpasswd -m sha-512\n\nPaste the output (starts with $6$) into the Password Hash field.",
            "Generating a password hash", MessageBoxButtons.OK, MessageBoxIcon.Information);
        scroll.Controls.Add(hashHelp);
        y += 30;

        ctrl.SshPasswordAuth = Check("Allow password login via SSH (less secure — not recommended)", cfg.SshPasswordAuth);
        y += 26;

        // ── System ───────────────────────────────────────────────────────────
        Section("System");

        ctrl.Timezone = Field("Timezone:", cfg.Timezone);

        // ── Cluster IPs ───────────────────────────────────────────────────────
        Section("Cluster IPs  (added to /etc/hosts on this node)");

        ctrl.RK1Ip      = Field("TuringPiRK1 IP:",      cfg.RK1Ip);
        ctrl.Compute3Ip = Field("TuringPICompute3 IP:", cfg.Compute3Ip);
        ctrl.Compute4Ip = Field("TuringPICompute4 IP:", cfg.Compute4Ip);

        // ── Packages ─────────────────────────────────────────────────────────
        Section("Packages");

        ctrl.GlusterServer = Check("glusterfs-server",   cfg.InstallGlusterServer, 0);
        ctrl.GlusterClient = Check("glusterfs-client",   cfg.InstallGlusterClient, 150);
        ctrl.InstallDocker = Check("docker.io",          cfg.InstallDocker,         300);
        y += 28;

        ctrl.ExtraPackages = Field("Extra packages:", cfg.ExtraPackages);
        var hint = new Label { Text = "comma or space separated", Location = new Point(FX + FW + 8, y - 20), AutoSize = true, ForeColor = Color.Gray, Font = new Font(Font.FontFamily, 8f) };
        scroll.Controls.Add(hint);

        // ── Extra Commands ────────────────────────────────────────────────────
        Section("Extra runcmd");
        ctrl.ExtraRuncmds = Field("One command per line:", cfg.ExtraRuncmds, height: 72);

        // ── Actions ───────────────────────────────────────────────────────────
        y += 6;
        var saveBtn  = Btn("💾  Save Config Files", FX,       y, 160, 34);
        var buildBtn = Btn("📀  Build Seed ISO",    FX + 168, y, 150, 34);
        var flashBtn = Btn("⚡  Flash Node",        FX + 326, y, 130, 34);

        saveBtn.BackColor  = Color.FromArgb(230, 242, 255);
        buildBtn.BackColor = Color.FromArgb(230, 242, 255);
        flashBtn.BackColor = Color.FromArgb(255, 235, 230);

        saveBtn.Click  += (_, _) => SaveNodeConfig(cfg.NodeNumber);
        buildBtn.Click += (_, _) => _ = BuildSeedIso(cfg.NodeNumber);
        flashBtn.Click += (_, _) => _ = FlashNode(cfg.NodeNumber);

        scroll.Controls.AddRange([saveBtn, buildBtn, flashBtn]);
        y += 44;

        scroll.AutoScrollMinSize = new Size(0, y + 20);
        return scroll;
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    private NodeConfig ReadControls(int nodeNumber)
    {
        var cfg  = Array.Find(_configs, c => c.NodeNumber == nodeNumber)!;
        var ctrl = _nodeCtrl[nodeNumber];

        cfg.Hostname             = ctrl.Hostname.Text.Trim();
        cfg.ImageType            = ctrl.ImageType.SelectedItem?.ToString() ?? "cm4";
        cfg.Username             = ctrl.Username.Text.Trim();
        cfg.SshPublicKey         = ctrl.SshKey.Text.Trim();
        cfg.PasswordHash         = ctrl.PasswordHash.Text.Trim();
        cfg.SshPasswordAuth      = ctrl.SshPasswordAuth.Checked;
        cfg.Timezone             = ctrl.Timezone.Text.Trim();
        cfg.RK1Ip                = ctrl.RK1Ip.Text.Trim();
        cfg.Compute3Ip           = ctrl.Compute3Ip.Text.Trim();
        cfg.Compute4Ip           = ctrl.Compute4Ip.Text.Trim();
        cfg.InstallGlusterServer = ctrl.GlusterServer.Checked;
        cfg.InstallGlusterClient = ctrl.GlusterClient.Checked;
        cfg.InstallDocker        = ctrl.InstallDocker.Checked;
        cfg.ExtraPackages        = ctrl.ExtraPackages.Text.Trim();
        cfg.ExtraRuncmds         = ctrl.ExtraRuncmds.Text.Trim();
        return cfg;
    }

    private void SaveNodeConfig(int nodeNumber)
    {
        var repoPath = _repoPathBox.Text.Trim();
        if (!ValidateRepoPath(repoPath)) return;

        var cfg     = ReadControls(nodeNumber);
        var nodeDir = Path.Combine(repoPath, "nodes", $"node{nodeNumber}");
        Directory.CreateDirectory(nodeDir);

        File.WriteAllText(Path.Combine(nodeDir, "user-data"),   CloudInitService.GenerateUserData(cfg));
        File.WriteAllText(Path.Combine(nodeDir, "meta-data"),   CloudInitService.GenerateMetaData(cfg));
        File.WriteAllText(Path.Combine(nodeDir, "config.json"),
            JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

        Log($"[node{nodeNumber}] Saved user-data, meta-data, config.json → {nodeDir}");
        SetStatus($"Node {nodeNumber} config saved.");
    }

    private async Task BuildSeedIso(int nodeNumber)
    {
        var repoPath = _repoPathBox.Text.Trim();
        if (!ValidateRepoPath(repoPath)) return;

        SaveNodeConfig(nodeNumber);
        SwitchToOutput();
        SetStatus($"Building seed ISO for node {nodeNumber}…");

        var wslRepo = WslService.ToWslPath(repoPath);
        var cmd     = $"cd '{wslRepo}' && mkdir -p seeds && " +
                      $"cloud-localds seeds/node{nodeNumber}-seed.iso " +
                      $"nodes/node{nodeNumber}/user-data nodes/node{nodeNumber}/meta-data";

        Log($"\n── Building seed ISO for node {nodeNumber} ──");
        var wsl = new WslService(_wslDistroBox.Text.Trim());
        await wsl.RunAsync(cmd, Log);

        var isoPath = Path.Combine(repoPath, "seeds", $"node{nodeNumber}-seed.iso");
        if (File.Exists(isoPath))
        {
            Log($"✔ Seed ISO ready: {isoPath}");
            SetStatus($"Seed ISO for node {nodeNumber} ready.");
        }
        else
        {
            Log("✘ Seed ISO not found — check output above. Is cloud-image-utils installed in WSL?");
            SetStatus("Seed ISO build may have failed.");
        }
    }

    private async Task FlashNode(int nodeNumber)
    {
        var repoPath = _repoPathBox.Text.Trim();
        if (!ValidateRepoPath(repoPath)) return;

        var cfg     = ReadControls(nodeNumber);
        var imgPath = Path.Combine(repoPath, "images", $"ubuntu-{cfg.ImageType}-base.img");

        if (!File.Exists(imgPath))
        {
            MessageBox.Show(
                $"Base image not found:\n{imgPath}\n\nDownload it to images/ — see README.md for links.",
                "Image Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(
            $"Flash Node {nodeNumber} ({cfg.Hostname})?\n\n" +
            $"The node will be powered off, flashed with {cfg.ImageType} image, and rebooted.",
            "Confirm Flash", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SwitchToOutput();
        SetStatus($"Flashing node {nodeNumber}…");

        var wslRepo = WslService.ToWslPath(repoPath);
        var cmd     = $"cd '{wslRepo}' && bash flash.sh {nodeNumber} {cfg.ImageType} --inject";

        Log($"\n── Flashing node {nodeNumber} ({cfg.Hostname}) ──");
        var wsl = new WslService(_wslDistroBox.Text.Trim());
        await wsl.RunAsync(cmd, Log);

        Log($"── Flash command completed. Monitor with: tpi uart -n {nodeNumber} ──");
        SetStatus($"Flash command sent for node {nodeNumber}.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool ValidateRepoPath(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return true;
        MessageBox.Show("Please set a valid Repo Path first.", "Repo Path Required",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void SwitchToOutput() =>
        _tabs.SelectedIndex = _tabs.TabPages.Count - 1;

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

    private void BrowseRepo(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description        = "Select the BaumConfigure repository folder",
            UseDescriptionForTitle = true,
            InitialDirectory   = string.IsNullOrEmpty(_repoPathBox.Text) ? "" : _repoPathBox.Text,
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _repoPathBox.Text = dlg.SelectedPath;
            TryLoadNodeConfigs(dlg.SelectedPath);
        }
    }

    // ── Settings persistence ──────────────────────────────────────────────────
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(
                new AppSettings { RepoPath = _repoPathBox.Text, WslDistro = _wslDistroBox.Text },
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
            if (!string.IsNullOrEmpty(s.RepoPath))  _repoPathBox.Text  = s.RepoPath;
            if (!string.IsNullOrEmpty(s.WslDistro)) _wslDistroBox.Text = s.WslDistro;
            if (!string.IsNullOrEmpty(s.RepoPath))  TryLoadNodeConfigs(s.RepoPath);
        }
        catch { /* ignore */ }
    }

    private void TryLoadNodeConfigs(string repoPath)
    {
        foreach (var cfg in _configs)
        {
            var jsonPath = Path.Combine(repoPath, "nodes", $"node{cfg.NodeNumber}", "config.json");
            if (!File.Exists(jsonPath)) continue;
            try
            {
                var loaded = JsonSerializer.Deserialize<NodeConfig>(File.ReadAllText(jsonPath));
                if (loaded == null || !_nodeCtrl.TryGetValue(cfg.NodeNumber, out var ctrl)) continue;

                ctrl.Hostname.Text              = loaded.Hostname;
                ctrl.ImageType.SelectedItem     = loaded.ImageType;
                ctrl.Username.Text              = loaded.Username;
                ctrl.SshKey.Text                = loaded.SshPublicKey;
                ctrl.PasswordHash.Text          = loaded.PasswordHash;
                ctrl.SshPasswordAuth.Checked    = loaded.SshPasswordAuth;
                ctrl.Timezone.Text              = loaded.Timezone;
                ctrl.RK1Ip.Text                 = loaded.RK1Ip;
                ctrl.Compute3Ip.Text            = loaded.Compute3Ip;
                ctrl.Compute4Ip.Text            = loaded.Compute4Ip;
                ctrl.GlusterServer.Checked      = loaded.InstallGlusterServer;
                ctrl.GlusterClient.Checked      = loaded.InstallGlusterClient;
                ctrl.InstallDocker.Checked      = loaded.InstallDocker;
                ctrl.ExtraPackages.Text         = loaded.ExtraPackages;
                ctrl.ExtraRuncmds.Text          = loaded.ExtraRuncmds;
            }
            catch { /* ignore corrupt config.json */ }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────
    private static Label Lbl(string text, int x, int y) =>
        new() { Text = text, Location = new Point(x, y), AutoSize = true };

    private static Button Btn(string text, int x, int y, int w, int h) =>
        new() { Text = text, Location = new Point(x, y), Width = w, Height = h, FlatStyle = FlatStyle.Flat };
}

// ── Per-node control references ───────────────────────────────────────────────
internal class NodeControls
{
    public TextBox   Hostname        = null!;
    public ComboBox  ImageType       = null!;
    public TextBox   Username        = null!;
    public TextBox   SshKey          = null!;
    public TextBox   PasswordHash    = null!;
    public CheckBox  SshPasswordAuth = null!;
    public TextBox   Timezone        = null!;
    public TextBox   RK1Ip           = null!;
    public TextBox   Compute3Ip      = null!;
    public TextBox   Compute4Ip      = null!;
    public CheckBox  GlusterServer   = null!;
    public CheckBox  GlusterClient   = null!;
    public CheckBox  InstallDocker   = null!;
    public TextBox   ExtraPackages   = null!;
    public TextBox   ExtraRuncmds    = null!;
}
