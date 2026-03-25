using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArchiveViewer.Dialogs;

public partial class ColorPickerDialog : Window
{
    public string SelectedColor { get; private set; }

    public ColorPickerDialog(string currentColor)
    {
        InitializeComponent();
        SelectedColor = currentColor;

        foreach (var colorHex in Theme.ColorChoices)
        {
            var btn = new Button
            {
                Width = 36, Height = 36,
                Margin = new Thickness(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)!),
                BorderThickness = new Thickness(3),
                BorderBrush = colorHex == currentColor
                    ? Brushes.White
                    : Theme.BorderBrush,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var hex = colorHex;
            btn.Click += (_, _) =>
            {
                SelectedColor = hex;
                DialogResult = true;
            };
            ColorPanel.Children.Add(btn);
        }
    }
}
