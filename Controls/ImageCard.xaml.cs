using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArchiveViewer.Controls;

public partial class ImageCard : UserControl
{
    public int Index { get; set; }
    public string FileName { get; set; } = "";
    private bool _isSelected;
    private bool _isRangeSelected;

    public event Action<int, MouseButtonEventArgs>? CardClicked;
    public event Action<int>? CardDoubleClicked;

    public ImageCard()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public void Setup(int index, string filename, BitmapSource? thumbnail, int thumbSize)
    {
        Index = index;
        FileName = filename;

        if (thumbnail != null)
            ThumbImage.Source = thumbnail;

        // Truncate filename: if > 20 chars, show "..." + last 17 chars
        var displayName = filename.Length > 20
            ? "..." + filename[^17..]
            : filename;
        NameLabel.Text = displayName;
        BadgeText.Text = $"#{index + 1}";
    }

    public void UpdateThumbnail(BitmapSource? thumbnail)
    {
        ThumbImage.Source = thumbnail;
    }

    public void SetSelected(bool selected, bool isRange = false)
    {
        _isSelected = selected;
        _isRangeSelected = isRange;
        UpdateBorder();
    }

    private void UpdateBorder()
    {
        if (_isSelected)
        {
            CardBorder.BorderBrush = _isRangeSelected
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22c55e")!)
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a855f7")!);
            CardBorder.Background = _isRangeSelected
                ? new SolidColorBrush(Color.FromArgb(40, 34, 197, 94))
                : new SolidColorBrush(Color.FromArgb(40, 168, 85, 247));
        }
        else
        {
            CardBorder.BorderBrush = Brushes.Transparent;
            CardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2a3e")!);
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isSelected)
            CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7c3aed")!);
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isSelected)
            CardBorder.BorderBrush = Brushes.Transparent;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            CardDoubleClicked?.Invoke(Index);
        else
            CardClicked?.Invoke(Index, e);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
}
