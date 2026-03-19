using System.Windows;

namespace ArchiveViewer.Dialogs;

public partial class ProgressDialog : Window
{
    public ProgressDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    public void UpdateProgress(double percent, string text)
    {
        Progress.IsIndeterminate = false;
        Progress.Value = percent;
        PercentText.Text = text;
    }
}
