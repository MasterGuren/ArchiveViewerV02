using System.Windows;
using System.Windows.Media;

namespace ArchiveViewer;

public record ThemePreset(
    string Name,
    string Bg, string Panel, string Accent, string Select,
    string Hover, string Text, string Subtext, string Border);

public static class Theme
{
    // Available presets
    public static readonly ThemePreset[] Presets =
    [
        new("ダークピンク",    "#3d1a30", "#502842", "#e91e63", "#f06292", "#603050", "#fce4ec", "#d4a0b0", "#7a4868"),
        new("ダークパープル",  "#1e1e2e", "#2a2a3e", "#7c3aed", "#a855f7", "#3a3a4e", "#e2e8f0", "#94a3b8", "#3f3f5e"),
        new("ダークブルー",    "#1a2332", "#243042", "#3b82f6", "#60a5fa", "#2e3e52", "#e2eaf0", "#94a8b8", "#3f5068"),
        new("ダークグリーン",  "#1a2e1e", "#243e28", "#22c55e", "#4ade80", "#2e4a32", "#e2f0e8", "#94b8a3", "#3f5e48"),
        new("ダーク",          "#1e1e1e", "#2a2a2a", "#6366f1", "#818cf8", "#3a3a3a", "#e2e2e2", "#a0a0a0", "#404040"),
        new("ピンク",          "#ffe0ec", "#ffc1d6", "#e91e63", "#c2185b", "#ffb0c8", "#4a1028", "#7a3050", "#e090a8"),
        new("ライトパープル",  "#f0e6ff", "#e0d0f8", "#7c3aed", "#6d28d9", "#d4c0f0", "#2e1a4a", "#5a3a80", "#c0a8e0"),
        new("ライトブルー",    "#e6f0ff", "#d0e0f8", "#3b82f6", "#2563eb", "#c0d4f0", "#1a2e4a", "#3a5a80", "#a8c0e0"),
    ];

    public const string DefaultTheme = "ダークピンク";

    // Current colors (updated by ApplyTheme)
    public static Color BgColor { get; private set; }
    public static Color PanelColor { get; private set; }
    public static Color AccentColor { get; private set; }
    public static Color SelectColor { get; private set; }
    public static Color HoverColor { get; private set; }
    public static Color TextColor { get; private set; }
    public static Color SubtextColor { get; private set; }
    public static Color BorderColor { get; private set; }
    public static readonly Color RangeSelectColor = (Color)ColorConverter.ConvertFromString("#22c55e")!;

    // Brushes (mutable — Color property updated on theme change)
    public static readonly SolidColorBrush BgBrush = new();
    public static readonly SolidColorBrush PanelBrush = new();
    public static readonly SolidColorBrush AccentBrush = new();
    public static readonly SolidColorBrush SelectBrush = new();
    public static readonly SolidColorBrush HoverBrush = new();
    public static readonly SolidColorBrush TextBrush = new();
    public static readonly SolidColorBrush SubtextBrush = new();
    public static readonly SolidColorBrush BorderBrush = new();
    public static readonly SolidColorBrush RangeSelectBrush = new(RangeSelectColor);
    public static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    // Action color palette (12 colors)
    public static readonly string[] ColorChoices =
    [
        "#ef4444", "#f97316", "#f59e0b", "#eab308",
        "#22c55e", "#14b8a6", "#3b82f6", "#6366f1",
        "#a855f7", "#ec4899", "#7f1d1d", "#64748b"
    ];

    // Thumbnail sizes
    public const int ThumbSizeDefault = 480;
    public const int ThumbSizeMin = 80;
    public const int ThumbSizeMax = 480;
    public const int ThumbSizeStep = 40;

    // Supported extensions
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff"];
    public static readonly string[] ArchiveExtensions = [".zip", ".rar", ".7z", ".cbz", ".cbr"];
    public static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".wmv", ".webm", ".mov", ".flv", ".m4v"];

    // Font
    public const string FontFamily = "Yu Gothic UI";

    public static string CurrentThemeName { get; private set; } = DefaultTheme;

    public static void ApplyTheme(string name)
    {
        var preset = System.Array.Find(Presets, p => p.Name == name) ?? Presets[0];
        CurrentThemeName = preset.Name;

        BgColor = ParseColor(preset.Bg);
        PanelColor = ParseColor(preset.Panel);
        AccentColor = ParseColor(preset.Accent);
        SelectColor = ParseColor(preset.Select);
        HoverColor = ParseColor(preset.Hover);
        TextColor = ParseColor(preset.Text);
        SubtextColor = ParseColor(preset.Subtext);
        BorderColor = ParseColor(preset.Border);

        BgBrush.Color = BgColor;
        PanelBrush.Color = PanelColor;
        AccentBrush.Color = AccentColor;
        SelectBrush.Color = SelectColor;
        HoverBrush.Color = HoverColor;
        TextBrush.Color = TextColor;
        SubtextBrush.Color = SubtextColor;
        BorderBrush.Color = BorderColor;

        // Update Application-level DynamicResources
        var res = Application.Current.Resources;
        res["ThemeBg"] = new SolidColorBrush(BgColor);
        res["ThemePanel"] = new SolidColorBrush(PanelColor);
        res["ThemeAccent"] = new SolidColorBrush(AccentColor);
        res["ThemeSelect"] = new SolidColorBrush(SelectColor);
        res["ThemeHover"] = new SolidColorBrush(HoverColor);
        res["ThemeText"] = new SolidColorBrush(TextColor);
        res["ThemeSubtext"] = new SolidColorBrush(SubtextColor);
        res["ThemeBorder"] = new SolidColorBrush(BorderColor);
        // Semi-transparent overlays
        res["ThemeBgOverlay"] = new SolidColorBrush(Color.FromArgb(0xCC, BgColor.R, BgColor.G, BgColor.B));
        res["ThemePanelOverlay"] = new SolidColorBrush(Color.FromArgb(0xCC, PanelColor.R, PanelColor.G, PanelColor.B));
    }

    private static Color ParseColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}
