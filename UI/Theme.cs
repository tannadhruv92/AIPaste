using System.Drawing;
using Microsoft.Win32;

namespace AIPaste.UI;

/// <summary>
/// Centralised colours, fonts and metrics for the indigo "Transform Studio" theme.
/// Supports switchable Light/Dark palettes plus a System option that follows Windows.
/// </summary>
internal static class Theme
{
    /// <summary>The currently selected theme mode (Light, Dark, or System).</summary>
    public static ThemeMode Mode { get; private set; }

    /// <summary>True when the effective palette is dark.</summary>
    public static bool IsDark { get; private set; }

    // Surfaces
    public static Color Bg { get; private set; }
    public static Color RailBg { get; private set; }
    public static Color Surface { get; private set; }
    public static Color Surface2 { get; private set; }
    public static Color Surface3 { get; private set; }
    public static Color Border { get; private set; }
    public static Color BorderStrong { get; private set; }

    // Text
    public static Color Text { get; private set; }
    public static Color TextDim { get; private set; }
    public static Color TextMuted { get; private set; }

    // Accents
    public static Color Accent { get; private set; }
    public static Color Accent2 { get; private set; }
    public static Color AccentInk { get; private set; }
    public static Color AccentGlow { get; private set; }
    public static Color AccentSoft { get; private set; }
    /// <summary>Solid background used for selected cards/rows.</summary>
    public static Color AccentSelected { get; private set; }

    // Status
    public static Color Success { get; private set; }
    public static Color SuccessSoft { get; private set; }
    public static Color Danger { get; private set; }
    public static Color Warn { get; private set; }

    static Theme() => ApplyMode(ThemeMode.System);

    /// <summary>
    /// Applies the given theme mode, resolving System against the Windows apps theme,
    /// and assigns every colour token from the Light or Dark palette.
    /// </summary>
    public static void ApplyMode(ThemeMode mode)
    {
        Mode = mode;
        bool dark = mode == ThemeMode.Dark
                    || (mode == ThemeMode.System && !SystemUsesLightTheme());
        IsDark = dark;

        if (dark)
        {
            // Surfaces
            Bg = Color.FromArgb(15, 16, 20);
            RailBg = Color.FromArgb(11, 12, 16);
            Surface = Color.FromArgb(22, 24, 30);
            Surface2 = Color.FromArgb(28, 31, 39);
            Surface3 = Color.FromArgb(35, 38, 47);
            Border = Color.FromArgb(44, 48, 58);
            BorderStrong = Color.FromArgb(59, 64, 76);
            // Text
            Text = Color.FromArgb(236, 236, 238);
            TextDim = Color.FromArgb(163, 166, 176);
            TextMuted = Color.FromArgb(109, 113, 124);
            // Accents
            Accent = Color.FromArgb(99, 102, 241);
            Accent2 = Color.FromArgb(139, 92, 246);
            AccentInk = Color.FromArgb(255, 255, 255);
            AccentGlow = Color.FromArgb(90, 99, 102, 241);
            AccentSoft = Color.FromArgb(46, 99, 102, 241);
            AccentSelected = Color.FromArgb(35, 39, 68);
            // Status
            Success = Color.FromArgb(52, 211, 153);
            SuccessSoft = Color.FromArgb(40, 52, 211, 153);
            Danger = Color.FromArgb(248, 113, 113);
            Warn = Color.FromArgb(251, 191, 36);
        }
        else
        {
            // Surfaces
            Bg = Color.FromArgb(246, 247, 249);
            RailBg = Color.FromArgb(238, 240, 244);
            Surface = Color.FromArgb(255, 255, 255);
            Surface2 = Color.FromArgb(255, 255, 255);
            Surface3 = Color.FromArgb(238, 240, 244);
            Border = Color.FromArgb(230, 231, 238);
            BorderStrong = Color.FromArgb(215, 217, 227);
            // Text
            Text = Color.FromArgb(24, 26, 32);
            TextDim = Color.FromArgb(92, 96, 107);
            TextMuted = Color.FromArgb(146, 150, 161);
            // Accents
            Accent = Color.FromArgb(99, 102, 241);
            Accent2 = Color.FromArgb(139, 92, 246);
            AccentInk = Color.FromArgb(255, 255, 255);
            AccentGlow = Color.FromArgb(90, 99, 102, 241);
            AccentSoft = Color.FromArgb(30, 99, 102, 241);
            AccentSelected = Color.FromArgb(236, 236, 252);
            // Status
            Success = Color.FromArgb(22, 163, 74);
            SuccessSoft = Color.FromArgb(36, 22, 163, 74);
            Danger = Color.FromArgb(220, 38, 38);
            Warn = Color.FromArgb(217, 119, 6);
        }
    }

    /// <summary>
    /// Reads the Windows "AppsUseLightTheme" preference from the registry.
    /// Returns true (light) on any error or when the value is missing.
    /// </summary>
    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is null)
                return true;
            return (int)value != 0;
        }
        catch
        {
            return true;
        }
    }

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
