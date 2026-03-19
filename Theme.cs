using System.Windows.Media;

namespace ArchiveViewer;

public static class Theme
{
    // Main colors
    public static readonly Color BgColor = (Color)ColorConverter.ConvertFromString("#1e1e2e")!;
    public static readonly Color PanelColor = (Color)ColorConverter.ConvertFromString("#2a2a3e")!;
    public static readonly Color AccentColor = (Color)ColorConverter.ConvertFromString("#7c3aed")!;
    public static readonly Color SelectColor = (Color)ColorConverter.ConvertFromString("#a855f7")!;
    public static readonly Color HoverColor = (Color)ColorConverter.ConvertFromString("#3a3a4e")!;
    public static readonly Color TextColor = (Color)ColorConverter.ConvertFromString("#e2e8f0")!;
    public static readonly Color SubtextColor = (Color)ColorConverter.ConvertFromString("#94a3b8")!;
    public static readonly Color BorderColor = (Color)ColorConverter.ConvertFromString("#3f3f5e")!;
    public static readonly Color RangeSelectColor = (Color)ColorConverter.ConvertFromString("#22c55e")!;

    // Brushes
    public static readonly SolidColorBrush BgBrush = new(BgColor);
    public static readonly SolidColorBrush PanelBrush = new(PanelColor);
    public static readonly SolidColorBrush AccentBrush = new(AccentColor);
    public static readonly SolidColorBrush SelectBrush = new(SelectColor);
    public static readonly SolidColorBrush HoverBrush = new(HoverColor);
    public static readonly SolidColorBrush TextBrush = new(TextColor);
    public static readonly SolidColorBrush SubtextBrush = new(SubtextColor);
    public static readonly SolidColorBrush BorderBrush = new(BorderColor);
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
}
