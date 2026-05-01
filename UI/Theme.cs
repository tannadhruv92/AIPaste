using System.Drawing;

namespace AIPaste.UI;

/// <summary>
/// Centralised colours, fonts and metrics for the redesigned dark theme.
/// Mirrors the values from docs/proposals/final-design.html.
/// </summary>
internal static class Theme
{
    // Surfaces
    public static readonly Color Bg = Color.FromArgb(13, 13, 16);          // #0d0d10
    public static readonly Color RailBg = Color.FromArgb(10, 10, 12);      // #0a0a0c
    public static readonly Color Surface = Color.FromArgb(24, 24, 27);     // #18181b
    public static readonly Color Surface2 = Color.FromArgb(31, 31, 35);    // #1f1f23
    public static readonly Color Surface3 = Color.FromArgb(39, 39, 42);    // #27272a
    public static readonly Color Border = Color.FromArgb(46, 46, 51);      // #2e2e33
    public static readonly Color BorderStrong = Color.FromArgb(63, 63, 70);// #3f3f46

    // Text
    public static readonly Color Text = Color.FromArgb(244, 244, 245);     // #f4f4f5
    public static readonly Color TextDim = Color.FromArgb(161, 161, 170);  // #a1a1aa
    public static readonly Color TextMuted = Color.FromArgb(113, 113, 122);// #71717a

    // Accents
    public static readonly Color Accent = Color.FromArgb(167, 139, 250);   // #a78bfa
    public static readonly Color Accent2 = Color.FromArgb(129, 140, 248); // #818cf8
    public static readonly Color AccentInk = Color.FromArgb(26, 22, 37);  // text on accent fill (#1a1625)
    public static readonly Color AccentGlow = Color.FromArgb(90, 167, 139, 250);
    public static readonly Color AccentSoft = Color.FromArgb(40, 167, 139, 250);

    // Status
    public static readonly Color Success = Color.FromArgb(52, 211, 153);   // #34d399
    public static readonly Color SuccessSoft = Color.FromArgb(40, 52, 211, 153);
    public static readonly Color Danger = Color.FromArgb(248, 113, 113);   // #f87171
    public static readonly Color Warn = Color.FromArgb(251, 191, 36);      // #fbbf24

    // Fonts
    public static readonly string FontFamily = "Segoe UI Variable Display";
    public static readonly string FontFamilyFallback = "Segoe UI";
    public static readonly string MonoFontFamily = "Cascadia Code";
    public static readonly string MonoFontFallback = "Consolas";

    public static Font Body() => new(ResolveFont(FontFamily, FontFamilyFallback), 9.5f, FontStyle.Regular);
    public static Font BodyBold() => new(ResolveFont(FontFamily, FontFamilyFallback), 9.5f, FontStyle.Bold);
    public static Font Title() => new(ResolveFont(FontFamily, FontFamilyFallback), 11f, FontStyle.Bold);
    public static Font Small() => new(ResolveFont(FontFamily, FontFamilyFallback), 8.25f, FontStyle.Regular);
    public static Font SmallBold() => new(ResolveFont(FontFamily, FontFamilyFallback), 8.25f, FontStyle.Bold);
    public static Font Mono() => new(ResolveFont(MonoFontFamily, MonoFontFallback), 9f, FontStyle.Regular);

    private static string ResolveFont(string preferred, string fallback)
    {
        try
        {
            using var test = new Font(preferred, 9f);
            // GDI+ silently substitutes; check the resolved family.
            return string.Equals(test.FontFamily.Name, preferred, System.StringComparison.OrdinalIgnoreCase)
                ? preferred
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    // Metrics
    public const int RailWidth = 56;
    public const int StatusBarHeight = 28;
    public const int TopBarHeight = 48;
    public const int CornerRadius = 8;
    public const int CornerRadiusLg = 12;
    public const int CornerRadiusPill = 999;
}
