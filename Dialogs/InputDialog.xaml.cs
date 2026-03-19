using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class InputDialog : Window
{
    public string InputText => InputBox.Text;

    public InputDialog(string title, string prompt = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Focus();
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
