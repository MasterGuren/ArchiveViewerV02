using System.Windows;
using System.Windows.Controls;

namespace ArchiveViewer.Dialogs;

public partial class NameInputDialog : Window
{
    private readonly string _stem;
    public string GeneratedFileName { get; private set; } = "output.zip";

    public NameInputDialog(string originalStem)
    {
        InitializeComponent();
        _stem = originalStem;
        OriginalName.Text = originalStem;
        UpdatePreview();
        AuthorBox.Focus();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(AuthorBox.Text)) parts.Add(AuthorBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(TitleBox.Text)) parts.Add(TitleBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(EpisodeBox.Text)) parts.Add(EpisodeBox.Text.Trim());
        parts.Add(_stem);

        GeneratedFileName = parts.Count > 1 || parts[0] != _stem
            ? string.Join("_", parts) + ".zip"
            : _stem + ".zip";

        if (parts.Count == 1 && parts[0] == _stem && string.IsNullOrWhiteSpace(AuthorBox.Text)
            && string.IsNullOrWhiteSpace(TitleBox.Text) && string.IsNullOrWhiteSpace(EpisodeBox.Text))
            GeneratedFileName = "output.zip";

        PreviewName.Text = GeneratedFileName;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
