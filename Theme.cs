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
        // ダーク系
        new("ダークピンク",    "#3d1a30", "#502842", "#e91e63", "#f06292", "#603050", "#fce4ec", "#d4a0b0", "#7a4868"),
        new("ダークパープル",  "#1e1e2e", "#2a2a3e", "#7c3aed", "#a855f7", "#3a3a4e", "#e2e8f0", "#94a3b8", "#3f3f5e"),
        new("ダークブルー",    "#1a2332", "#243042", "#3b82f6", "#60a5fa", "#2e3e52", "#e2eaf0", "#94a8b8", "#3f5068"),
        new("ダークグリーン",  "#1a2e1e", "#243e28", "#22c55e", "#4ade80", "#2e4a32", "#e2f0e8", "#94b8a3", "#3f5e48"),
        new("ダーク",          "#1e1e1e", "#2a2a2a", "#6366f1", "#818cf8", "#3a3a3a", "#e2e2e2", "#a0a0a0", "#404040"),
        new("ダークレッド",    "#2e1010", "#3e1a1a", "#ef4444", "#f87171", "#4e2a2a", "#fde8e8", "#c09090", "#5e3838"),
        new("ダークオレンジ",  "#2e1e0e", "#3e2a16", "#f97316", "#fb923c", "#4e3a26", "#fff0e0", "#c0a080", "#5e4830"),
        new("ダークティール",  "#0e2e2e", "#163e3e", "#14b8a6", "#2dd4bf", "#264e4e", "#e0f0f0", "#80c0b8", "#305e58"),
        new("ダークイエロー",  "#2e2a0e", "#3e3616", "#eab308", "#facc15", "#4e4626", "#fff8e0", "#c0b080", "#5e5430"),
        new("ダークシアン",    "#0e2030", "#162e40", "#06b6d4", "#22d3ee", "#263e50", "#e0f4f8", "#80b0c8", "#304e60"),
        // ライト系
        new("ピンク",          "#ffe0ec", "#ffc1d6", "#e91e63", "#c2185b", "#ffb0c8", "#4a1028", "#7a3050", "#e090a8"),
        new("ライトパープル",  "#f0e6ff", "#e0d0f8", "#7c3aed", "#6d28d9", "#d4c0f0", "#2e1a4a", "#5a3a80", "#c0a8e0"),
        new("ライトブルー",    "#e6f0ff", "#d0e0f8", "#3b82f6", "#2563eb", "#c0d4f0", "#1a2e4a", "#3a5a80", "#a8c0e0"),
        new("ライトグリーン",  "#e6ffe6", "#c8f0c8", "#22c55e", "#16a34a", "#b0e0b0", "#1a3a1a", "#3a6a3a", "#90c890"),
        new("ライトレッド",    "#ffe6e6", "#ffc8c8", "#ef4444", "#dc2626", "#ffb0b0", "#3a1010", "#6a3030", "#e0a0a0"),
        new("ライトオレンジ",  "#fff0e0", "#ffdcb8", "#f97316", "#ea580c", "#ffc8a0", "#3a1e0a", "#6a4020", "#e0b090"),
        new("ライト",          "#f5f5f5", "#e8e8e8", "#6366f1", "#4f46e5", "#d8d8d8", "#1e1e1e", "#606060", "#c0c0c0"),
        // 原色背景
        new("原色レッド",      "#cc0000", "#a00000", "#ffcc00", "#ffe066", "#b80000", "#ffffff", "#ffcccc", "#880000"),
        new("原色ブルー",      "#0000cc", "#0000a0", "#ffcc00", "#ffe066", "#0000b8", "#ffffff", "#ccccff", "#000088"),
        new("原色グリーン",    "#008800", "#006600", "#ffcc00", "#ffe066", "#007700", "#ffffff", "#ccffcc", "#004400"),
        new("原色イエロー",    "#ccaa00", "#b89900", "#cc0000", "#ff3333", "#aa8800", "#1a1a00", "#665500", "#998800"),
        new("原色パープル",    "#6600cc", "#5500a0", "#ffcc00", "#ffe066", "#5a00b8", "#ffffff", "#ddccff", "#440088"),
        new("原色オレンジ",    "#cc5500", "#a04400", "#0044cc", "#3366ff", "#b84a00", "#ffffff", "#ffe0cc", "#883300"),
        // ピンク系
        new("原色ピンク",      "#ff1493", "#dd1080", "#ffffff", "#ffe0f0", "#cc1078", "#ffffff", "#ffd0e8", "#aa0060"),
        new("ホットピンク",    "#ff69b4", "#ff4da0", "#6600cc", "#8833ee", "#ee5aa0", "#1a0020", "#660044", "#dd3388"),
        new("ベビーピンク",    "#ffb6c1", "#ffa0b0", "#cc1060", "#e01870", "#ff90a0", "#2a0010", "#6a2040", "#ee88a0"),
        new("フューシャ",      "#ff00ff", "#dd00dd", "#ffff00", "#ffff66", "#cc00cc", "#ffffff", "#ffd0ff", "#aa00aa"),
        new("ローズ",          "#cc2266", "#aa1a55", "#ffd700", "#ffe044", "#991850", "#ffffff", "#ffccdd", "#881044"),
        new("サーモンピンク",  "#ff8080", "#ee6e6e", "#2244aa", "#3366cc", "#dd6060", "#1a0000", "#602020", "#cc5050"),
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

    // Action color palette
    public static readonly string[] ColorChoices =
    [
        // Primary / vivid
        "#FF0000", "#FF4500", "#FF8C00", "#FFD700",
        "#00FF00", "#00CED1", "#0000FF", "#8B00FF",
        "#FF00FF", "#FF1493",
        // Tailwind tones
        "#ef4444", "#f97316", "#f59e0b", "#eab308",
        "#22c55e", "#14b8a6", "#3b82f6", "#6366f1",
        "#a855f7", "#ec4899",
        // Dark / muted
        "#7f1d1d", "#64748b", "#1e3a5f", "#2d1b69"
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
        // Contrast text colors (auto white/black based on luminance)
        res["ThemePanelText"] = new SolidColorBrush(ContrastTextColor(PanelColor));
        res["ThemeAccentText"] = new SolidColorBrush(ContrastTextColor(AccentColor));
        res["ThemeHoverText"] = new SolidColorBrush(ContrastTextColor(HoverColor));
        // Semi-transparent overlays
        res["ThemeBgOverlay"] = new SolidColorBrush(Color.FromArgb(0xCC, BgColor.R, BgColor.G, BgColor.B));
        res["ThemePanelOverlay"] = new SolidColorBrush(Color.FromArgb(0xCC, PanelColor.R, PanelColor.G, PanelColor.B));
    }

    /// <summary>背景色の輝度に応じて白/黒を返す</summary>
    public static Color ContrastTextColor(Color bg)
    {
        var luminance = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
        return luminance < 140 ? Colors.White : Colors.Black;
    }

    private static Color ParseColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}
