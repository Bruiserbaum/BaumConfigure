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
}
