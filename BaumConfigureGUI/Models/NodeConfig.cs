namespace BaumConfigureGUI.Models;

public class NodeConfig
{
    public string Hostname        { get; set; } = "ubuntu-server";
    public string Username        { get; set; } = "ubuntu";
    public string Password        { get; set; } = "";   // plain-text — hashed before writing cloud-init
    public string PasswordHash    { get; set; } = "";   // sha-512 hash, populated on build
    public string Timezone        { get; set; } = "America/New_York";
    public bool   InstallDocker   { get; set; } = false;
    public bool   InstallK8s      { get; set; } = false;
    public bool   InstallPortainer{ get; set; } = false;
    public string ExtraPackages   { get; set; } = "";
    public string ExtraRuncmds    { get; set; } = "";

    // ── SSH ───────────────────────────────────────────────────────────────────
    public bool   SshPasswordAuth       { get; set; } = true;

    // ── Tweaks ────────────────────────────────────────────────────────────────
    public bool   AutoPatch            { get; set; } = true;
    public bool   LogRotation          { get; set; } = true;
    public bool   WeeklyReboot         { get; set; } = false;
    public string DockerRestartPolicy  { get; set; } = "unless-stopped"; // no, unless-stopped, always, on-failure

    // ── Network ───────────────────────────────────────────────────────────────
    public bool   ConfigureNetwork { get; set; } = false;
    public string NetworkType      { get; set; } = "ethernet"; // "ethernet" or "wifi"
    public bool   UseDhcp          { get; set; } = true;
    public string StaticIp         { get; set; } = "";  // CIDR e.g. 192.168.1.10/24
    public string Gateway          { get; set; } = "";
    public string DnsServers       { get; set; } = "8.8.8.8,8.8.4.4";
    public string WifiSsid         { get; set; } = "";
    public string WifiPassword     { get; set; } = "";
}
