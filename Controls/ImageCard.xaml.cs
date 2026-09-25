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

    // サムネイルキャッシュ自体の画素数（thumbSizeに依存しない固有サイズ）。
    // 表示サイズはこれと枠から計算するので、ズーム時にビットマップを作り直さなくてよい。
    private int _srcW;
    private int _srcH;

    public event Action<int, MouseButtonEventArgs>? CardClicked;
    public event Action<int>? CardDoubleClicked;

    public ImageCard()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public void Setup(int index, string filename, BitmapSource? thumbnail, int srcW, int srcH, int thumbSize, string orient)
    {
        Index = index;
        FileName = filename;
        _srcW = srcW;
        _srcH = srcH;

        ApplyThumbSize(thumbSize, orient);

        if (thumbnail != null)
            UpdateThumbnail(thumbnail);

        // Truncate filename: if > 20 chars, show "..." + last 17 chars
        var displayName = filename.Length > 20
            ? "..." + filename[^17..]
            : filename;
        NameLabel.Text = displayName;
        BadgeText.Text = $"#{index + 1}";
    }

    /// <summary>表示サイズちょうどに作り直したビットマップを差し替える。</summary>
    public void UpdateThumbnail(BitmapSource? thumbnail)
    {
        ThumbImage.Source = thumbnail;
        // 実サイズで用意されたので高品質側に戻す
        RenderOptions.SetBitmapScalingMode(ThumbImage, BitmapScalingMode.Unspecified);
    }

    /// <summary>
    /// サムネイル枠のサイズを適用する。表示サイズを明示しておくことで、ズームや向きの
    /// 切り替えをビットマップの作り直しなし（レイアウトだけ）で行える。作り直しが済むまでの
    /// 間はWPFの引き伸ばしになるので、その区間だけ低コストな拡縮モードにしておく。
    /// </summary>
    public void ApplyThumbSize(int thumbSize, string orient)
    {
        if (_srcW <= 0 || _srcH <= 0)
        {
            // サムネイル生成に失敗した画像。従来同様サイズ指定なし（＝潰れたカード）のまま。
            ThumbImage.Width = double.NaN;
            ThumbImage.Height = double.NaN;
            return;
        }

        var (maxW, maxH) = Services.ThumbnailService.ThumbBox(thumbSize, orient);
        double scale = Math.Min((double)maxW / _srcW, (double)maxH / _srcH);
        if (scale > 1) scale = 1; // 元サイズを超える拡大はしない（従来の見た目を維持）

        ThumbImage.Width = Math.Max(1, Math.Round(_srcW * scale));
        ThumbImage.Height = Math.Max(1, Math.Round(_srcH * scale));
        RenderOptions.SetBitmapScalingMode(ThumbImage, BitmapScalingMode.LowQuality);
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
                ? Theme.RangeSelectBrush
                : Theme.SelectBrush;
            CardBorder.Background = _isRangeSelected
                ? new SolidColorBrush(Color.FromArgb(40, Theme.RangeSelectColor.R, Theme.RangeSelectColor.G, Theme.RangeSelectColor.B))
                : new SolidColorBrush(Color.FromArgb(40, Theme.SelectColor.R, Theme.SelectColor.G, Theme.SelectColor.B));
        }
        else
        {
            CardBorder.BorderBrush = Brushes.Transparent;
            CardBorder.Background = Theme.PanelBrush;
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isSelected)
            CardBorder.BorderBrush = Theme.AccentBrush;
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
