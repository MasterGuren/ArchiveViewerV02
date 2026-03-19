using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class ErrorDialog : Window
{
    public ErrorDialog(string message)
    {
        InitializeComponent();
        ErrorText.Text = message;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ErrorText.Text);
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
