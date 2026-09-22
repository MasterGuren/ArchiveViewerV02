using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class ConfirmDialog : Window
{
    /// <summary>okOnly: trueの場合「いいえ」ボタンを隠し、「はい」を「OK」にした案内専用ダイアログにする。</summary>
    public ConfirmDialog(string message, string title = "確認", bool okOnly = false)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        if (okOnly)
        {
            BtnYes.Content = "OK";
            BtnNo.Visibility = Visibility.Collapsed;
        }
        BtnYes.Focus();
    }

    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
