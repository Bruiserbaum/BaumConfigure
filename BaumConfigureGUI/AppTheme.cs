namespace BaumConfigureGUI;

internal static class AppTheme
{
    // ── Background palette ────────────────────────────────────────────────────
    public static readonly Color BgDeep      = Color.FromArgb(13,  13,  18);
    public static readonly Color BgMain      = Color.FromArgb(18,  18,  26);
    public static readonly Color BgPanel     = Color.FromArgb(22,  22,  32);
    public static readonly Color BgCard      = Color.FromArgb(30,  30,  44);
    public static readonly Color BgCardHover = Color.FromArgb(38,  38,  54);
    public static readonly Color BgInput     = Color.FromArgb(26,  26,  38);

    // ── Accent / status ───────────────────────────────────────────────────────
    public static readonly Color Accent      = Color.FromArgb(78,  131, 253);
    public static readonly Color AccentHover = Color.FromArgb(98,  151, 255);
    public static readonly Color Success     = Color.FromArgb(72,  199, 142);
    public static readonly Color Warning     = Color.FromArgb(255, 189,  46);
    public static readonly Color Danger      = Color.FromArgb(240,  80,  80);
    public static readonly Color Border      = Color.FromArgb(45,   45,  65);

    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly Color TextPrimary   = Color.FromArgb(230, 230, 240);
    public static readonly Color TextSecondary = Color.FromArgb(160, 160, 180);
    public static readonly Color TextMuted     = Color.FromArgb(100, 100, 120);

    // ── Fonts ─────────────────────────────────────────────────────────────────
    public static readonly Font FontTitle  = new("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font FontHeader = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font FontBody   = new("Segoe UI", 10f);
    public static readonly Font FontBold   = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font FontSmall  = new("Segoe UI",  9f);
    public static readonly Font FontButton = new("Segoe UI",  9f, FontStyle.Bold);
    public static readonly Font FontMono   = new("Consolas",  9f);
}
