namespace BaumConfigureGUI.Models;

public class NodeConfig
{
    public int    NodeNumber          { get; set; }
    public string Hostname            { get; set; } = "";
    public string ImageType           { get; set; } = "cm4";   // rk1 | cm4
    public string Username            { get; set; } = "ubuntu";
    public string SshPublicKey        { get; set; } = "";
    public string PasswordHash        { get; set; } = "";
    public bool   SshPasswordAuth     { get; set; } = false;
    public bool   DisableRoot         { get; set; } = true;
    public string Timezone            { get; set; } = "America/New_York";
    public string RK1Ip               { get; set; } = "";
    public string Compute3Ip          { get; set; } = "";
    public string Compute4Ip          { get; set; } = "";
    public bool   InstallGlusterServer{ get; set; } = false;
    public bool   InstallGlusterClient{ get; set; } = false;
    public bool   InstallDocker       { get; set; } = false;
    public string ExtraPackages       { get; set; } = "";
    public string ExtraRuncmds        { get; set; } = "";
}
