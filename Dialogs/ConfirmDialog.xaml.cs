using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string message, string title = "確認")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
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
