namespace BaumConfigureGUI.Models;

public class AppSettings
{
    public string    BaseImagePath    { get; set; } = "";
    public string    OutputDir        { get; set; } = "";
    public string    WslDistro        { get; set; } = "Ubuntu";
    public DateTime  LastUpdateCheck  { get; set; } = DateTime.MinValue;
    public NodeConfig? LastConfig     { get; set; }
}
