using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class ConflictDialog : Window
{
    public string Result { get; private set; } = "skip";

    public ConflictDialog(string fileName, string srcSize, string dstSize, string comparison)
    {
        InitializeComponent();
        FileNameText.Text = $"同名ファイルが存在します: {fileName}";
        SizeInfo.Text = $"移動元: {srcSize}  /  移動先: {dstSize}";
        CompareText.Text = comparison;
    }

    private void BtnOverwrite_Click(object sender, RoutedEventArgs e) { Result = "overwrite"; Close(); }
    private void BtnRename_Click(object sender, RoutedEventArgs e) { Result = "rename"; Close(); }
    private void BtnSkip_Click(object sender, RoutedEventArgs e) { Result = "skip"; Close(); }
}
