using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArchiveViewer.Dialogs;

public class ActionEditResult
{
    public string Label { get; set; } = "";
    public string ColorHex { get; set; } = "";
}

public partial class ActionEditDialog : Window
{
    public ActionEditResult? Result { get; private set; }
    public bool Deleted { get; private set; }
    private string _selectedColor;

    public ActionEditDialog(string? label = null, string? color = null)
    {
        InitializeComponent();
        _selectedColor = color ?? Theme.ColorChoices[0];

        if (label != null)
        {
            LabelBox.Text = label;
            BtnDelete.Visibility = Visibility.Visible;
        }

        BuildColorPalette();
        UpdatePreview();
        LabelBox.Focus();
    }

    private void BuildColorPalette()
    {
        foreach (var colorHex in Theme.ColorChoices)
        {
            var btn = new Button
            {
                Width = 32, Height = 32,
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)!),
                BorderThickness = new Thickness(2),
                BorderBrush = colorHex == _selectedColor
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3f3f5e")!),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var hex = colorHex;
            btn.Click += (_, _) =>
            {
                _selectedColor = hex;
                BuildColorPalette();
                UpdatePreview();
            };
            ColorPanel.Children.Add(btn);
        }
    }

    private void UpdatePreview()
    {
        PreviewBtn.Content = string.IsNullOrEmpty(LabelBox.Text) ? "プレビュー" : LabelBox.Text;
        PreviewBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedColor)!);
    }

    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LabelBox.Text)) return;
        Result = new ActionEditResult { Label = LabelBox.Text, ColorHex = _selectedColor };
        DialogResult = true;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("このアクションを削除しますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            Deleted = true;
            DialogResult = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
