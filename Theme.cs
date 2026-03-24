using System.Windows.Media;

namespace ArchiveViewer;

public static class Theme
{
    // Main colors
    public static readonly Color BgColor = (Color)ColorConverter.ConvertFromString("#3d1a30")!;
    public static readonly Color PanelColor = (Color)ColorConverter.ConvertFromString("#502842")!;
    public static readonly Color AccentColor = (Color)ColorConverter.ConvertFromString("#e91e63")!;
    public static readonly Color SelectColor = (Color)ColorConverter.ConvertFromString("#f06292")!;
    public static readonly Color HoverColor = (Color)ColorConverter.ConvertFromString("#603050")!;
    public static readonly Color TextColor = (Color)ColorConverter.ConvertFromString("#fce4ec")!;
    public static readonly Color SubtextColor = (Color)ColorConverter.ConvertFromString("#d4a0b0")!;
    public static readonly Color BorderColor = (Color)ColorConverter.ConvertFromString("#7a4868")!;
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
